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
	private bool _remoteCloneSimulated;

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

		if (_session.Role == SessionRole.Host)
		{
			if (_localBody is not null)
			{
				PublishBodyState(_localBody, _session.LocalPlayer, publishRemote: false);
			}

			if (_remoteCloneBody is not null && _remoteCloneSimulated)
			{
				PublishBodyState(_remoteCloneBody, _session.RemotePlayer!, publishRemote: true);
			}
		}
		else
		{
			SessionStatePump.Apply(_session.LocalPlayer, _localBody);
			SessionStatePump.Apply(_session.RemotePlayer, _remoteCloneBody);
		}
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
		_session.InputReceived -= OnInputReceived;
		_session.SessionEnded -= OnSessionEnded;
		Instance = null;
	}

	void IDisposable.Dispose() => ((ICuoService)this).Dispose();

	// ---- Session wiring ----

	internal void BindToSession()
	{
		_session.RemoteJoined += OnRemoteJoined;
		_session.RemoteLeft += OnRemoteLeft;
		_session.InputReceived += OnInputReceived;
		_session.SessionEnded += OnSessionEnded;
	}

	private void OnRemoteJoined(PlayerEntity remote)
	{
		var simulated = _session.Role == SessionRole.Host;
		_remoteCloneSimulated = simulated;
		_remoteCloneBody = RemoteBodyFactory.CreateRemoteBody(
			remote, simulated, _localBody?.transform.position ?? Vector2.zero, _log);
		_log.LogInformation("Remote body created for {SteamId} (simulated: {Simulated}).",
			remote.SteamId, simulated);
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

	private void OnInputReceived(PlayerEntity remote)
	{
		if (_remoteCloneBody is null || !_remoteCloneSimulated)
		{
			return;
		}

		_remoteCloneBody.moveDir = new Vector2(remote.MoveDir.X, remote.MoveDir.Y);
		_remoteCloneBody.crouching = remote.Crouching;
		if (remote.JumpQueued)
		{
			remote.JumpQueued = false;
			if (_remoteCloneBody.standing && _remoteCloneBody.conscious)
			{
				_remoteCloneBody.Jump();
			}
		}
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
		_session.ReportSceneState(inWorld ? SceneStateType.InWorld : SceneStateType.InMenu, sceneName);
	}

	// ---- State shuttling ----

	private void PublishBodyState(Body body, PlayerEntity target, bool publishRemote)
	{
		var pos = body.transform.position;
		var look = body.targetLookPos;
		var vel = body.rb.velocity;
		var state = (
			new NetVector2(pos.x, pos.y),
			new NetVector2(look.x, look.y),
			new NetVector2(vel.x, vel.y),
			body.isRight, body.standing, body.alive, body.conscious, body.crouching);
		if (publishRemote)
		{
			_session.PublishRemoteState(target, state.Item1, state.Item2, state.Item3,
				state.Item4, state.Item5, state.Item6, state.Item7, state.Item8);
		}
		else
		{
			_session.PublishLocalState(state.Item1, state.Item2, state.Item3,
				state.Item4, state.Item5, state.Item6, state.Item7, state.Item8);
		}
	}

	/// <summary>Guest side: input from the HandleInput patch → session.</summary>
	internal void SubmitGuestInput(float moveX, float moveY, bool jump, bool crouch) => _session.SubmitLocalInput(new NetVector2(moveX, moveY), jump, crouch);

	/// <summary>Called from the StartRun patch (see PreRunScriptPatches).</summary>
	internal void OnStartRun()
	{
		if (_session.Role == SessionRole.Host || _session.Role == SessionRole.None)
		{
			CaptureWorldParams();
		}
		else if (_session.WorldParams is not null)
		{
			ApplyWorldParams(_session.WorldParams);
		}
	}
}
