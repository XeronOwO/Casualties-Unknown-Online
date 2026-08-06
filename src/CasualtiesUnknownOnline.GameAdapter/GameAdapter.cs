using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;
using IGameAdapter = CasualtiesUnknownOnline.Runtime.GameAdapter.IGameAdapter;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Game Adapter for the current Casualties Unknown (Demo) build (architecture.md
/// §4). The only layer that knows game types: it hooks input, freezes/simulates
/// player bodies, clones remote players and captures/applies world-start
/// parameters. The sync semantics live in the Runtime (SessionService); this
/// class only shuttles state between game objects and the session.
/// </summary>
public sealed class GameAdapter : IGameAdapter, ICuoService
{
	/// <summary>Static access for Harmony patches (they have no DI).</summary>
	public static GameAdapter? Instance { get; private set; }

	private readonly SessionService _session;
	private readonly ILogger<GameAdapter> _log;
	private Harmony? _harmony;

	private Body? _localBody;
	private Body? _remoteCloneBody;
	private bool _inWorld;

	public GameAdapter(SessionService session, ILogger<GameAdapter> log)
	{
		_session = session;
		_log = log;
		Instance = this;
	}

	public string CapabilityReport { get; private set; } = "Not probed";

	/// <summary>Guest-side input interception active (in a live session as guest).</summary>
	internal bool IsGuestMode => _session.Role == SessionRole.Guest && _session.SessionActive;

	public bool ProbeGame()
	{
		var playerCamera = typeof(PlayerCamera);
		var body = typeof(Body);
		var preRun = typeof(PreRunScript);
		var worldGen = typeof(WorldGeneration);
		var ok = playerCamera is not null && body is not null && preRun is not null && worldGen is not null;
		CapabilityReport = ok
			? "PlayerCamera/Body/PreRunScript/WorldGeneration: OK"
			: "PlayerCamera/Body/PreRunScript/WorldGeneration: MISSING";
		return ok;
	}

	public bool Install()
	{
		try
		{
			_harmony = new Harmony("CasualtiesUnknownOnline.GameAdapter");
			_harmony.PatchAll(typeof(GameAdapter).Assembly);
			_log.LogInformation("Game Adapter patches installed.");
			return true;
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Game Adapter patch install failed.");
			_harmony?.UnpatchSelf();
			_harmony = null;
			return false;
		}
	}

	public void Uninstall()
	{
		_harmony?.UnpatchSelf();
		_harmony = null;
	}

	public void CaptureWorldParams()
	{
		// Host side: snapshot what defines a run before generation consumes the RNG.
		var randomState = RandomStateSerializer.Serialize(Random.state);
		var runSettings = HarmonyTraverse.ReadRunSettings();
		_session.PublishWorldParams(new WorldStartParams
		{
			RandomState = randomState,
			RunSettings = runSettings,
		});
		_log.LogInformation("Captured world params ({StateBytes} bytes, {SettingCount} settings).",
			randomState.Length, runSettings?.Count ?? 0);
	}

	public void ApplyWorldParams(WorldStartParams parameters)
	{
		// Guest side: restore the host's RNG state + run settings so local world
		// generation produces the same world (docs/game-internals.md).
		Random.state = RandomStateSerializer.Deserialize(parameters.RandomState);
		if (parameters.RunSettings is not null)
		{
			HarmonyTraverse.WriteRunSettings(parameters.RunSettings);
		}

		_log.LogInformation("Applied host world params ({StateBytes} bytes).", parameters.RandomState.Length);
	}

	void ICuoService.Initialize()
	{
		BindToSession();
		if (ProbeGame())
		{
			Install();
		}
		else
		{
			_log.LogError("Game Adapter probe failed — CUO multiplayer unavailable.");
		}
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		UpdateSceneState();
		if (!_session.EntitySyncActive)
		{
			return;
		}

		// Both sides: publish the local body's state (host → PlayerState
		// broadcast, guest → PlayerStateReport to the host) and render the
		// remote clone from the peer's reported state. NO remote-side
		// simulation anywhere — each player simulates only its own body.
		if (_localBody is not null)
		{
			PublishBodyState(_localBody);
		}

		SessionStatePump.Apply(_session.RemotePlayer, _remoteCloneBody);
	}

	void ICuoService.Stop() => Uninstall();

	void ICuoService.Dispose()
	{
		if (_remoteCloneBody is not null)
		{
			UnityEngine.Object.Destroy(_remoteCloneBody.transform.parent.gameObject);
		}

		_session.RemoteJoined -= OnRemoteJoined;
		_session.RemoteLeft -= OnRemoteLeft;
		_session.SessionEnded -= OnSessionEnded;
		_session.SessionActivated -= OnSessionActivated;
		Instance = null;
	}

	void IDisposable.Dispose() => ((ICuoService)this).Dispose();

	// ---- Session wiring ----

	internal void BindToSession()
	{
		_session.RemoteJoined += OnRemoteJoined;
		_session.RemoteLeft += OnRemoteLeft;
		_session.SessionEnded += OnSessionEnded;
		_session.SessionActivated += OnSessionActivated;
	}

	/// <summary>
	/// Re-report the scene state (with position) once the handshake completes —
	/// if we entered the world before connecting, the earlier report was not
	/// sent (no session yet) and the host would spawn our clone at (0,0).
	/// </summary>
	private void OnSessionActivated()
	{
		if (!_inWorld || _localBody is null)
		{
			return;
		}

		var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		var pos = new NetVector2(_localBody.transform.position.x, _localBody.transform.position.y);
		_session.ReportSceneState(SceneStateType.InWorld, sceneName, pos);
	}

	private void OnRemoteJoined(PlayerEntity remote)
	{
		// Host: spawn the guest's clone at the guest's reported spawn point.
		// Guest: the host's clone renders at the host position from PlayerJoin.
		// Both are frozen render proxies fed by the peer's state reports.
		var anchor = _session.Role == SessionRole.Host
			? new Vector2(remote.ReportedSpawnPos.X, remote.ReportedSpawnPos.Y)
			: new Vector2(remote.Position.X, remote.Position.Y);
		_remoteCloneBody = RemoteBodyFactory.CreateRemoteBody(remote, anchor, _log);
		_log.LogInformation("Remote body created for {SteamId}.", remote.SteamId);
	}

	private void OnRemoteLeft(PlayerEntity remote)
	{
		if (_remoteCloneBody is not null)
		{
			UnityEngine.Object.Destroy(_remoteCloneBody.transform.parent.gameObject);
			_remoteCloneBody = null;
		}
		_log.LogInformation("Remote body destroyed for {SteamId}.", remote.SteamId);
	}

	private void OnSessionEnded()
	{
		if (_remoteCloneBody is not null)
		{
			UnityEngine.Object.Destroy(_remoteCloneBody.transform.parent.gameObject);
			_remoteCloneBody = null;
		}
	}

	// ---- Scene state ----

	private void UpdateSceneState()
	{
		var inWorld = PlayerCamera.main is not null && WorldGeneration.world is not null
			&& !HarmonyTraverse.IsGenerating();
		if (inWorld == _inWorld)
		{
			if (inWorld && _localBody is null)
			{
				_localBody = PlayerCamera.main!.body;
			}

			return;
		}

		_inWorld = inWorld;
		_localBody = inWorld ? PlayerCamera.main!.body : null;
		var sceneName = inWorld ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name : "PreGen";
		var pos = inWorld && _localBody is not null
			? new NetVector2(_localBody.transform.position.x, _localBody.transform.position.y)
			: (NetVector2?)null;
		_session.ReportSceneState(inWorld ? SceneStateType.InWorld : SceneStateType.InMenu, sceneName, pos);
	}

	// ---- State shuttling ----

	private void PublishBodyState(Body body)
	{
		var pos = body.transform.position;
		var look = body.targetLookPos;
		var vel = body.rb.velocity;
		_session.PublishLocalState(
			new NetVector2(pos.x, pos.y),
			new NetVector2(look.x, look.y),
			new NetVector2(vel.x, vel.y),
			body.isRight, body.standing, body.alive, body.conscious, body.crouching);
	}

	/// <summary>Gate from the StartRun patch — returns false to block the run start.</summary>
	internal bool OnStartRun()
	{
		if (_session.Role == SessionRole.Guest && _session.SessionActive && _session.WorldParams is null)
		{
			_log.LogWarning("Cannot start a run: host world params not received yet — retry in a few seconds.");
			return false;
		}

		return true;
	}

	/// <summary>
	/// Called at the WorldGeneration.GenerateWorld boundary — the true start of
	/// generation. Host captures its RNG state (and any randomness consumed
	/// before this point is baked in); guest restores the host's state so both
	/// generate the identical world.
	/// </summary>
	internal void OnWorldGenerate()
	{
		if (_session.Role == SessionRole.Host || _session.Role == SessionRole.None)
		{
			CaptureWorldParams();
		}
		else if (_session.WorldParams is not null)
		{
			ApplyWorldParams(_session.WorldParams);
		}
		else
		{
			_log.LogWarning("World generation started without host world params — world will not match!");
		}
	}
}
