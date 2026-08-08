using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using HarmonyLib;
using MapsterMapper;
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
	private readonly IMapper _mapper;
	private Harmony? _harmony;

	private Body? _localBody;
	private Body? _remoteCloneBody; // single clone until Step 4 (per-member map)
	private bool _inWorld;

	private const float CharacterReportInterval = 1f; // guest → host character snapshot (1 Hz)
	private long _nextCharacterReportMs;
	private CharacterDataMsg? _pendingRestore; // guest side: host-sent restore, applied once the body exists

	public GameAdapter(SessionService session, ILogger<GameAdapter> log, IMapper mapper)
	{
		_session = session;
		_log = log;
		_mapper = mapper;
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
		CharacterDataMapper.Configure();
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
		// Publish local state even before entity sync activates — the host's
		// PlayerJoin carries the local position, which was (0,0) before sync
		// because publishing only ran after activation.
		if (_localBody != null) // Unity object — == (is null misses scene-reload-destroyed)
		{
			PublishBodyState(_localBody);
			ReportCharacterDataIfDue();
			TryApplyCharacterRestore();
		}

		if (!_session.EntitySyncActive)
		{
			return;
		}

		// Both sides render the remote clone from the peer's reported state.
		// NO remote-side simulation anywhere — each player simulates only its
		// own body. Single clone until Step 4 turns this into a per-member map.
		var remote = _session.RemotePlayers.FirstOrDefault();
		if (remote is null || !remote.InWorld)
		{
			return; // remote is in a menu/loading — clone stays paused
		}

		SessionStatePump.Apply(remote, _remoteCloneBody);
		LogClonePosition();
	}

	private long _nextCloneLogMs;

	/// <summary>Periodic clone diagnostics (1 Hz) — where the remote proxy actually is.</summary>
	private void LogClonePosition()
	{
		var nowMs = Environment.TickCount;
		if (nowMs < _nextCloneLogMs)
		{
			return;
		}

		_nextCloneLogMs = nowMs + 1000;
		// == null on the Unity clone: a scene reload destroys it and
		// reference-comparison (?.) would throw on access.
		var pos = _remoteCloneBody != null
			? _remoteCloneBody.transform.position
			: Vector3.zero;
		var remote = _session.RemotePlayers.FirstOrDefault();
		var reported = remote is not null
			? new Vector2(remote.Position.X, remote.Position.Y)
			: Vector2.zero;
		_log.LogDebug("Clone: at ({PX:F1}, {PY:F1}), reported ({RX:F1}, {RY:F1}), active {Active}",
			pos.x, pos.y, reported.x, reported.y, _remoteCloneBody != null && _remoteCloneBody.gameObject.activeInHierarchy);
	}

	void ICuoService.Stop() => Uninstall();

	void ICuoService.Dispose()
	{
		// == null on the Unity clone (is null would miss a scene-reload-destroyed object).
		if (_remoteCloneBody != null)
		{
			UnityEngine.Object.Destroy(_remoteCloneBody.transform.parent.gameObject);
		}

		_session.RemoteJoined -= OnRemoteJoined;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= OnSessionEnded;
		_session.SessionActivated -= OnSessionActivated;
		_session.BlockDamagedReceived -= OnRemoteBlockDamaged;
		_session.CharacterDataReceived -= OnCharacterDataReceived;
		Instance = null;
	}

	void IDisposable.Dispose() => ((ICuoService)this).Dispose();

	// ---- Session wiring ----

	internal void BindToSession()
	{
		_session.RemoteJoined += OnRemoteJoined;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
		_session.SessionEnded += OnSessionEnded;
		_session.SessionActivated += OnSessionActivated;
		_session.BlockDamagedReceived += OnRemoteBlockDamaged;
		_session.CharacterDataReceived += OnCharacterDataReceived;
	}

	/// <summary>
	/// Re-report the scene state (with position) once the handshake completes —
	/// if we entered the world before connecting, the earlier report was not
	/// sent (no session yet) and the host would spawn our clone at (0,0).
	/// </summary>
	private void OnSessionActivated()
	{
		if (!_inWorld || _localBody == null) // Unity object — ==
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
		// Menu round-trips destroy the clone (RemoteSceneChanged) and rebuild
		// it here — position resumes via the state stream's Lerp.
		// NOTE: == null on Unity objects — a scene reload destroys the clone
		// and reference-comparison (is null) would miss it.
		if (_remoteCloneBody == null)
		{
			var anchor = _session.Role == SessionRole.Host
				? new Vector2(remote.ReportedSpawnPos.X, remote.ReportedSpawnPos.Y)
				: new Vector2(remote.Position.X, remote.Position.Y);
			_remoteCloneBody = RemoteBodyFactory.CreateRemoteBody(remote, anchor, _log);
			_log.LogInformation("Remote body created for {SteamId}.", remote.SteamId);
		}
		else
		{
			_log.LogInformation("Remote re-joined — clone reused for {SteamId}.", remote.SteamId);
		}
	}

	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (!inWorld)
		{
			// The member left the world: destroy its render clone — it carries
			// no state (character data lives in the host's save dict; the
			// entity buffer lives in SessionService), and a rejoin rebuilds it
			// via RemoteJoined. Menu round-trips are rare, so the rebuild cost
			// is irrelevant. NOTE: == null on Unity objects — a scene reload
			// destroys the clone and reference-comparison (is null / ?.) would
			// miss it; write the explicit check so the formatter keeps it
			// (a bare call would be simplified back to ?.).
			if (_remoteCloneBody != null)
			{
				UnityEngine.Object.Destroy(_remoteCloneBody.transform.parent.gameObject);
				_remoteCloneBody = null;
			}

			// The host leaving the world ends the world itself (host
			// authority): a guest must not keep playing inside a world whose
			// owner is gone — pull it back to the main menu. Only when we are
			// actually in the world; after the load the normal UpdateSceneState
			// flow re-reports InMenu to the host.
			if (steamId == _session.HostSteamId
				&& _session.Role == SessionRole.Guest && _inWorld && PlayerCamera.main != null)
			{
				_log.LogInformation("Host left the world — returning to main menu.");
				PlayerCamera.main.ToMainMenu();
			}
		}
		else if (steamId == _session.HostSteamId
			&& _session.Role == SessionRole.Guest && !_inWorld
			&& PreRunScript.instance != null)
		{
			// The host entering the world: the guest follows automatically
			// (the world belongs to the host — a guest sitting in the menu
			// joins the run). StartRun is the game's own entry; guests without
			// the basic course get the tutorial warning instead (its flow).
			// The host's clone is rebuilt by RemoteJoined after the join flow.
			_log.LogInformation("Host entered the world — starting a run to follow.");
			PreRunScript.instance.StartRun();
		}

		_log.LogInformation(inWorld
			? "Remote entered the world — clone rebuilt on rejoin."
			: "Remote not in world (menu or disconnected) — clone destroyed.");
	}

	private void OnSessionEnded()
	{
		// == null on the Unity clone (is null would miss a scene-reload-destroyed object).
		if (_remoteCloneBody != null)
		{
			UnityEngine.Object.Destroy(_remoteCloneBody.transform.parent.gameObject);
			_remoteCloneBody = null;
		}
	}

	// ---- Block damage sync (local compute, remote verify/sync) ----

	private bool _applyingRemoteBlockDamage;

	/// <summary>
	/// Called from the DamageBlock patch after a LOCAL block damage was applied:
	/// report it so the peer applies the same damage at the same world position.
	/// </summary>
	internal void OnBlockDamaged(Vector2 pos, float dmg)
	{
		if (_applyingRemoteBlockDamage || !_session.SessionActive)
		{
			return;
		}

		_session.SendBlockDamaged(new NetVector2(pos.x, pos.y), dmg);
	}

	/// <summary>The peer damaged a block — apply it locally (remote verify/sync).</summary>
	private void OnRemoteBlockDamaged(NetVector2 pos, float dmg)
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return;
		}

		_applyingRemoteBlockDamage = true;
		try
		{
			WorldGeneration.world.DamageBlock(new Vector2(pos.X, pos.Y), dmg);
		}
		finally
		{
			_applyingRemoteBlockDamage = false;
		}
	}

	// ---- Character data (session-scoped save/restore, character-data-plan) ----

	private void OnCharacterDataReceived(CharacterDataMsg data)
	{
		// May arrive before the local body exists (still loading the run) —
		// apply once the game has spawned it (TryApplyCharacterRestore).
		_pendingRestore = data;
		_log.LogInformation("Received character restore ({Items} items).", data.Items.Count);
	}

	private void ReportCharacterDataIfDue()
	{
		var nowMs = Environment.TickCount;
		if (nowMs < _nextCharacterReportMs)
		{
			return;
		}

		_nextCharacterReportMs = nowMs + (long)(CharacterReportInterval * 1000f);
		_session.ReportCharacterData(CaptureCharacterData(_localBody!));
	}

	private void TryApplyCharacterRestore()
	{
		if (_pendingRestore is null)
		{
			return;
		}

		// Apply only once world generation finished: the game hands out the
		// starting supplies inside generation (WorldPlacePlayer), and the
		// restore wipes the slots first — applying during generation would
		// race that handout (observed: the default lantern ending up on the
		// ground instead of in the restored inventory).
		if (HarmonyTraverse.IsGenerating())
		{
			return;
		}

		ApplyCharacterData(_localBody!, _pendingRestore);
		_pendingRestore = null;
	}

	private CharacterDataMsg CaptureCharacterData(Body body)
	{
		var msg = new CharacterDataMsg
		{
			Skills = _mapper.Map<CharacterSkillsMsg>(body.skills),
			Health = _mapper.Map<CharacterHealthMsg>(body),
			HandSlot = body.handSlot,
		};

		// Limb has no Index field — Mapster maps the rest, the loop assigns it.
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limbMsg = _mapper.Map<CharacterLimbMsg>(body.limbs[i]);
			limbMsg.Index = i;
			msg.Limbs.Add(limbMsg);
		}

		// Items: id ↔ ItemId is a rename, not a case variant — keep it manual.
		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var item = body.GetItem(slot);
			if (item == null) // Unity object — ==
			{
				continue;
			}

			msg.Items.Add(new CharacterItemMsg
			{
				ItemId = item.id,
				Condition = item.condition,
				SlotIndex = slot,
			});
		}

		return msg;
	}

	private void ApplyCharacterData(Body body, CharacterDataMsg data)
	{
		_log.LogInformation("Applying character restore ({Items} items).", data.Items.Count);

		// Wipe the fresh-run default state first: this new run already got its
		// starting supplies (WorldGeneration.WorldPlacePlayer) and random vitals
		// (Body.Start) — restoring on top would duplicate items and leave
		// random hunger/thirst. Destroy (end-of-frame) is fine: the slots are
		// immediately re-filled and the old children vanish one frame later.
		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var holder = body.slots[slot].transform;
			for (var i = holder.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(holder.GetChild(i).gameObject);
			}
		}

		if (data.Skills is { } skills)
		{
			_mapper.Map(skills, body.skills);
			body.skills.UpdateExpBoundaries(); // min/max derive from STR/RES/INT (Skills.cs:61)
		}

		if (data.Health is { } health)
		{
			// Target-driven: only writable Body members that exist in the source
			// are touched — alive/conscious (derived properties, Body.cs:203/213)
			// are read-only and skipped automatically.
			_mapper.Map(health, body);
		}

		foreach (var limbData in data.Limbs)
		{
			if (limbData.Index < 0 || limbData.Index >= body.limbs.Length)
			{
				continue;
			}

			_mapper.Map(limbData, body.limbs[limbData.Index]);
		}

		foreach (var itemData in data.Items)
		{
			if (itemData.SlotIndex < 0 || itemData.SlotIndex >= body.slots.Length)
			{
				continue;
			}

			// Same spawn path as the game's save restore (SaveSystem.cs:304-329):
			// instantiate the prefab by id, set condition, hand it to the slot.
			var go = UnityEngine.Object.Instantiate((GameObject)Resources.Load(itemData.ItemId),
				body.transform.position, Quaternion.identity);
			var item = go.GetComponent<Item>();
			if (item == null) // Unity object — ==
			{
				UnityEngine.Object.Destroy(go);
				_log.LogWarning("Restore: {ItemId} has no Item component — skipped.", itemData.ItemId);
				continue;
			}

			item.condition = itemData.Condition;
			body.PickUpItem(item, itemData.SlotIndex, force: true);
		}

		if (data.HandSlot >= 0 && data.HandSlot < body.slots.Length)
		{
			body.handSlot = data.HandSlot;
		}
	}

	// ---- Scene state ----

	private void UpdateSceneState()
	{
		// == null on Unity singletons (is null misses scene-reload-destroyed objects).
		var inWorld = PlayerCamera.main != null && WorldGeneration.world != null
			&& !HarmonyTraverse.IsGenerating();
		if (inWorld == _inWorld)
		{
			if (inWorld && _localBody == null) // Unity object — ==
			{
				_localBody = PlayerCamera.main!.body;
			}

			return;
		}

		_inWorld = inWorld;
		_localBody = inWorld ? PlayerCamera.main!.body : null;
		var sceneName = inWorld ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name : "PreGen";
		var pos = inWorld && _localBody != null // Unity object — ==
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
		// Pose flags mirror the game's own pose rules:
		// - sitting: idle sit condition (Body.cs:3162), minus movingAllowed
		//   (private; sleeping is covered by the sleeping flag).
		// - sleeping: Body.cs:3961.
		// - climbing: currentClimbable (Body.cs:470).
		var sitting = body.idleTime > 12f && !body.exercising;
		_session.PublishLocalState(
			new NetVector2(pos.x, pos.y),
			new NetVector2(look.x, look.y),
			new NetVector2(vel.x, vel.y),
			body.isRight, body.standing, body.alive, body.conscious, body.crouching,
			sitting, body.sleeping, body.currentClimbable != null); // Unity object — ==
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
