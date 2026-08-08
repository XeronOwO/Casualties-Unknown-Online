using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
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
/// parameters. The sync semantics live in the Runtime domain services
/// (SessionService / EntitySyncService / CharacterDataStore / WorldService);
/// this class only shuttles state between game objects and the domains.
/// </summary>
public sealed class GameAdapter : IGameAdapter, ICuoService
{
	/// <summary>Static access for Harmony patches (they have no DI).</summary>
	public static GameAdapter? Instance { get; private set; }

	/// <summary>
	/// Set when the game was launched via a Steam friends "Join Game"
	/// (+connect_lobby): the content-warning/intro screen is skipped so the
	/// menu is usable immediately — the follow-host pump needs PreRunScript.
	/// </summary>
	public static bool SkipIntro { get; set; }

	private readonly SessionService _session;
	private readonly EntitySyncService _entities;
	private readonly CharacterDataStore _characterData;
	private readonly WorldService _world;
	private readonly ILogger<GameAdapter> _log;
	private readonly IMapper _mapper;
	private Harmony? _harmony;

	private Body? _localBody;
	private readonly Dictionary<ulong, Body> _remoteClones = []; // member SteamId → render clone
	private bool _inWorld;
	private bool _worldJoinPending; // guest: the host's enter instruction arrived while the menu was still loading
	private bool _startRunAuthorized; // set right before the WorldJoin-triggered StartRun, consumed by the gate

	private const float CharacterReportInterval = 1f; // guest → host character snapshot (1 Hz)
	private long _nextCharacterReportMs;
	private CharacterDataMsg? _pendingRestore; // guest side: host-sent restore, applied once the body exists
	private bool _restoreWipePending; // first pass wiped the slots (Destroy is end-of-frame) — items go in on the next frame

	public GameAdapter(SessionService session, EntitySyncService entities, CharacterDataStore characterData,
		WorldService world, ILogger<GameAdapter> log, IMapper mapper)
	{
		_session = session;
		_entities = entities;
		_characterData = characterData;
		_world = world;
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
		// Host side: snapshot what defines a run before generation consumes the
		// RNG. The world-defining fields (biome override/depth, total traveled)
		// were dead on the wire until this step — now captured with the RNG state.
		var randomState = RandomStateSerializer.Serialize(Random.state);
		var runSettings = HarmonyTraverse.ReadRunSettings();
		var biomeOverride = (byte)HarmonyTraverse.ReadBiomeOverride();
		var biomeDepth = (byte)HarmonyTraverse.ReadBiomeDepth();
		var totalTraveled = HarmonyTraverse.ReadTotalTraveled();
		_world.PublishWorldParams(new WorldStartParams
		{
			RandomState = randomState,
			RunSettings = runSettings,
			BiomeOverride = biomeOverride,
			BiomeDepth = biomeDepth,
			TotalTraveled = totalTraveled,
			// LoadedRun: no backing game field (PreRunScript.LoadRun is the
			// save-load flow — Phase 3 saves scope) — stays false on the wire.
		});
		_log.LogInformation("Captured world params ({StateBytes} bytes, {SettingCount} settings, "
			+ "biome {Biome}/{Depth}, traveled {Traveled}).",
			randomState.Length, runSettings?.Count ?? 0, biomeOverride, biomeDepth, totalTraveled);
	}

	public void ApplyWorldParams(WorldStartParams parameters)
	{
		// Guest side: restore the host's RNG state + run settings + world-defining
		// fields so local world generation produces the same world
		// (docs/game-internals.md).
		Random.state = RandomStateSerializer.Deserialize(parameters.RandomState);
		if (parameters.RunSettings is not null)
		{
			HarmonyTraverse.WriteRunSettings(parameters.RunSettings);
		}

		HarmonyTraverse.WriteBiomeOverride(parameters.BiomeOverride);
		HarmonyTraverse.WriteBiomeDepth(parameters.BiomeDepth);
		HarmonyTraverse.WriteTotalTraveled(parameters.TotalTraveled);

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
		UpdateGuestMenuState();
		if (_worldJoinPending)
		{
			TryStartWorldJoin();
		}

		// Publish local state even before entity sync activates — the host's
		// PlayerJoin carries the local position, which was (0,0) before sync
		// because publishing only ran after activation.
		if (_localBody != null) // Unity object — == (is null misses scene-reload-destroyed)
		{
			PublishBodyState(_localBody);
			ReportCharacterDataIfDue();
			TryApplyCharacterRestore();
		}

		if (!_entities.EntitySyncActive)
		{
			return;
		}

		// Both sides render remote clones from the reported states. NO remote-side
		// simulation anywhere — each player simulates only its own body.
		// Lazy per-member ensure: a roster join can arrive before the member's
		// world exists (the menu scene has no "Experiment" template), and members
		// can join mid-session — retrying every frame absorbs all ordering races.
		foreach (var remote in _entities.RemotePlayers)
		{
			if (!_session.IsRemoteInWorld(remote.SteamId))
			{
				continue; // in a menu/loading — no clone
			}

			// == null on Unity objects — a scene reload destroys the clone and
			// reference-comparison would miss it; retry creation next frame.
			if (!_remoteClones.TryGetValue(remote.SteamId, out var clone) || clone == null)
			{
				clone = RemoteBodyFactory.CreateRemoteBody(remote, AnchorFor(remote), _log);
				if (clone == null)
				{
					continue; // template unavailable — retry next frame
				}

				_remoteClones[remote.SteamId] = clone;
				_log.LogInformation("Remote body created for {SteamId}.", remote.SteamId);
			}

			SessionStatePump.Apply(remote, clone);
		}

		LogClonePosition();
	}

	private Vector2 AnchorFor(PlayerEntity remote) =>
		_session.Role == SessionRole.Host
			? new Vector2(_session.GetRemoteSpawnPos(remote.SteamId).X, _session.GetRemoteSpawnPos(remote.SteamId).Y)
			: new Vector2(remote.Position.X, remote.Position.Y);

	private long _nextCloneLogMs;

	/// <summary>Periodic clone diagnostics (1 Hz) — where the remote proxies actually are.</summary>
	private void LogClonePosition()
	{
		var nowMs = Environment.TickCount;
		if (nowMs < _nextCloneLogMs)
		{
			return;
		}

		_nextCloneLogMs = nowMs + 1000;
		if (_remoteClones.Count == 0)
		{
			return;
		}

		// KeyValuePair has no Deconstruct on net48 — iterate entries explicitly.
		foreach (var entry in _remoteClones)
		{
			var steamId = entry.Key;
			var clone = entry.Value;
			// == null on the Unity clone: a scene reload destroys it and
			// reference-comparison (?.) would throw on access.
			var pos = clone != null ? clone.transform.position : Vector3.zero;
			var remote = _entities.GetRemotePlayer(steamId);
			var reported = remote is not null
				? new Vector2(remote.Position.X, remote.Position.Y)
				: Vector2.zero;
			_log.LogDebug("Clone {SteamId}: at ({PX:F1}, {PY:F1}), reported ({RX:F1}, {RY:F1}), active {Active}",
				steamId, pos.x, pos.y, reported.x, reported.y, clone != null && clone.gameObject.activeInHierarchy);
		}
	}

	void ICuoService.Stop() => Uninstall();

	void IDisposable.Dispose()
	{
		// == null on the Unity clones (is null would miss scene-reload-destroyed objects).
		foreach (var clone in _remoteClones.Values)
		{
			if (clone != null)
			{
				UnityEngine.Object.Destroy(clone.transform.parent.gameObject);
			}
		}

		_remoteClones.Clear();

		_entities.RemoteJoined -= OnRemoteJoined;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.SessionEnded -= OnSessionEnded;
		_session.SessionActivated -= OnSessionActivated;
		_world.BlockDamagedReceived -= OnRemoteBlockDamaged;
		_world.WorldJoinReceived -= OnWorldJoin;
		_characterData.CharacterDataReceived -= OnCharacterDataReceived;
		Instance = null;
	}

	// ---- Session wiring ----

	internal void BindToSession()
	{
		_entities.RemoteJoined += OnRemoteJoined;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
		_session.SessionEnded += OnSessionEnded;
		_session.SessionActivated += OnSessionActivated;
		_world.BlockDamagedReceived += OnRemoteBlockDamaged;
		_world.WorldJoinReceived += OnWorldJoin;
		_characterData.CharacterDataReceived += OnCharacterDataReceived;
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

	private void OnRemoteJoined(PlayerEntity remote) =>
		// Clone creation is handled by the per-frame lazy ensure in Update —
		// the roster join can arrive before the member's world exists (the menu
		// scene has no "Experiment" template), so event-driven creation would
		// race. Log only; the pump creates and the anchor for host/guest differs.
		_log.LogInformation("Remote joined (clone ensured by the Update pump): {SteamId}.", remote.SteamId);

	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (!inWorld)
		{
			// The member left the world: destroy its render clone — it carries
			// no state (character data lives in the host's save store; the
			// entity buffer lives in EntitySyncService), and the Update pump
			// rebuilds it when the member re-enters. NOTE: == null on Unity
			// objects — a scene reload destroys the clone and reference
			// comparison (is null / ?.) would miss it.
			if (_remoteClones.TryGetValue(steamId, out var clone) && clone != null)
			{
				UnityEngine.Object.Destroy(clone.transform.parent.gameObject);
			}

			_remoteClones.Remove(steamId);

			// The host leaving the world ends the world itself (host
			// authority): a guest must not keep playing inside a world whose
			// owner is gone — pull it back to the main menu. Only when we are
			// actually in the world; after the load the normal UpdateSceneState
			// flow re-reports InMenu to the host.
			if (steamId == _session.HostSteamId && _session.Role == SessionRole.Guest)
			{
				_worldJoinPending = false; // a fresh "host entered" must re-arm the follow
				if (_inWorld && PlayerCamera.main != null) // Unity object — ==
				{
					_log.LogInformation("Host left the world — returning to main menu.");
					PlayerCamera.main.ToMainMenu();
				}
			}
		}

		_log.LogInformation(inWorld
			? "Remote entered the world — clone rebuilt on rejoin."
			: "Remote not in world (menu or disconnected) — clone destroyed.");
	}

	/// <summary>Guest side: the host told us to enter the world (WorldJoin). The
	/// menu may still be loading when it arrives (a right-click "Join Game"
	/// launches a fresh process) — wait for PreRunScript, then start the run.</summary>
	private void OnWorldJoin()
	{
		_worldJoinPending = true;
		TryStartWorldJoin();
	}

	private void TryStartWorldJoin()
	{
		if (!_worldJoinPending || _inWorld)
		{
			return;
		}

		if (PreRunScript.instance == null) // Unity object — == (menu still loading)
		{
			return;
		}

		_worldJoinPending = false;
		_startRunAuthorized = true; // the gate refuses unauthorised (manual) guest starts
		_log.LogInformation("World join received — starting a run to follow.");
		PreRunScript.instance.StartRun();
	}

	private void OnSessionEnded()
	{
		// == null on the Unity clones (is null would miss scene-reload-destroyed objects).
		foreach (var clone in _remoteClones.Values)
		{
			if (clone != null)
			{
				UnityEngine.Object.Destroy(clone.transform.parent.gameObject);
			}
		}

		_remoteClones.Clear();
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

		_world.SendBlockDamaged(new NetVector2(pos.x, pos.y), dmg);
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
		if (_pendingRestore is not null || _restoreWipePending)
		{
			return; // restoring: a fresh-run snapshot would overwrite the host's saved character data
		}

		var nowMs = Environment.TickCount;
		if (nowMs < _nextCharacterReportMs)
		{
			return;
		}

		_nextCharacterReportMs = nowMs + (long)(CharacterReportInterval * 1000f);
		_characterData.ReportCharacterData(CaptureCharacterData(_localBody!));
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

		if (_restoreWipePending)
		{
			// Second pass (next frame): the wipe's Destroy ran at the end of
			// the previous frame, so the slots are actually empty now and
			// PickUpItem succeeds — it silently refuses a non-empty slot
			// (Body.cs:1388), which stranded the restored items on the ground.
			ApplyRestoredItems(_localBody!, _pendingRestore);
			_pendingRestore = null;
			_restoreWipePending = false;
			return;
		}

		ApplyRestoredStatsAndWipe(_localBody!, _pendingRestore);
		_restoreWipePending = true;
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
		// Capture is recursive: container contents ride inside the parent item
		// (Contents), and [Saveable] component state (liquids, batteries, ammo,
		// …) rides along — the wire form of the official save's SavedItem +
		// component dictionaries (SaveSystem.SaveGame), so a restore is complete.
		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var item = body.GetItem(slot);
			if (item == null) // Unity object — ==
			{
				continue;
			}

			msg.Items.Add(CaptureItem(item, slot));
		}

		return msg;
	}

	private void ApplyRestoredStatsAndWipe(Body body, CharacterDataMsg data)
	{
		_log.LogInformation("Applying character restore ({Items} items).", data.Items.Count);

		// Wipe the fresh-run default state first: this new run already got its
		// starting supplies (WorldGeneration.WorldPlacePlayer) and random vitals
		// (Body.Start) — restoring on top would duplicate items and leave
		// random hunger/thirst. Destroy is end-of-frame; the items are re-added
		// on the next frame (TryApplyCharacterRestore's second pass), so the
		// slots are actually empty when PickUpItem runs — it silently refuses
		// a non-empty slot (Body.cs:1388) and the item would be stranded.
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
	}

	private void ApplyRestoredItems(Body body, CharacterDataMsg data)
	{
		foreach (var itemData in data.Items)
		{
			RestoreItem(itemData, body);
		}

		if (data.HandSlot >= 0 && data.HandSlot < body.slots.Length)
		{
			body.handSlot = data.HandSlot;
		}
	}

	// ---- Item capture/restore (complete state: SavedItem fields + [Saveable] components + container contents) ----

	/// <summary>Recursively captures one item: the SavedItem fields (condition/
	/// favourited/slot), the WaterContainerItem liquid stacks, the [Saveable]
	/// component states and the container contents.</summary>
	private CharacterItemMsg CaptureItem(Item item, int slotIndex)
	{
		var msg = new CharacterItemMsg
		{
			ItemId = item.id,
			Condition = item.condition,
			SlotIndex = slotIndex,
			Favourited = item.favourited,
			Liquids = CaptureLiquids(item),
			Components = CaptureSaveableComponents(item),
		};

		var container = item.GetComponent<Container>();
		if (container != null) // Unity object — ==
		{
			for (var i = 0; i < container.transform.childCount; i++)
			{
				var child = container.transform.GetChild(i).GetComponent<Item>();
				if (child != null) // Unity object — ==
				{
					msg.Contents.Add(CaptureItem(child, slotIndex));
				}
			}
		}

		return msg;
	}

	/// <summary>The WaterContainerItem's liquid stacks. The stack field is
	/// private (the public surface is query-only) — read by reflection;
	/// LiquidStack itself is public with public fields. A renamed/missing
	/// field must fail loudly, not silently drop the liquids.</summary>
	private List<LiquidStackMsg> CaptureLiquids(Item item)
	{
		var water = item.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			return [];
		}

		var stackField = typeof(WaterContainerItem).GetField("stack",
			BindingFlags.NonPublic | BindingFlags.Instance);
		if (stackField is null)
		{
			_log.LogWarning("WaterContainerItem.stack field not found — liquid sync disabled (game updated?).");
			return [];
		}

		var stack = (List<LiquidStack>?)stackField.GetValue(water);
		return stack is null ? [] : [.. stack.Select(s => new LiquidStackMsg
		{
			LiquidId = s.liquidId,
			Amount = s.amount,
		})];
	}

	/// <summary>Snapshots every [Saveable] component's simple-typed state —
	/// the wire form of the official save's per-item component dictionaries.
	/// Unity-reference fields are never serialized; WaterContainerItem is
	/// skipped (its state travels as Liquids).</summary>
	private List<ComponentStateMsg> CaptureSaveableComponents(Item item)
	{
		var states = new List<ComponentStateMsg>();
		foreach (var comp in item.GetComponents<Component>())
		{
			if (comp is WaterContainerItem) // Unity object — ==
			{
				continue; // handled by CaptureLiquids
			}

			if (comp.GetType().GetCustomAttribute<Saveable>(inherit: false) is null)
			{
				continue;
			}

			var fields = new List<ComponentFieldMsg>();
			foreach (var field in comp.GetType().GetFields(
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
			{
				if (field.IsStatic || field.IsInitOnly)
				{
					continue;
				}

				// Private state must be explicitly marked for serialization
				// (the Unity serializer's rule, which the game relies on).
				if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() is null)
				{
					continue;
				}

				var kind = ComponentFieldKind(field.FieldType);
				if (kind == 0)
				{
					continue; // unsupported kind (Unity references, custom types)
				}

				var value = field.GetValue(comp);
				fields.Add(new ComponentFieldMsg
				{
					Name = field.Name,
					Kind = kind,
					FloatValue = kind == 1 ? (float)value! : 0f,
					IntValue = kind == 2 ? (int)value! : 0,
					BoolValue = kind == 3 && (bool)value!,
					StringValue = kind == 4 ? (string)value! : "",
					StringList = kind == 5 ? (List<string>)value! : [],
				});
			}

			states.Add(new ComponentStateMsg { TypeName = comp.GetType().Name, Fields = fields });
		}

		return states;
	}

	private static int ComponentFieldKind(Type type)
	{
		if (type == typeof(float))
		{
			return 1;
		}

		if (type == typeof(int))
		{
			return 2;
		}

		if (type == typeof(bool))
		{
			return 3;
		}

		if (type == typeof(string))
		{
			return 4;
		}

		if (type == typeof(List<string>))
		{
			return 5;
		}

		return 0;
	}

	/// <summary>Restores one item (recursively): instantiate by id, apply the
	/// SavedItem fields, the liquid stacks, the component states and the
	/// container contents, then hand it to the slot — with the game's own
	/// restore semantics (SaveSystem.cs:304-329): a non-empty slot takes the
	/// item into its container instead of failing.</summary>
	private void RestoreItem(CharacterItemMsg itemData, Body body)
	{
		if (itemData.SlotIndex < 0 || itemData.SlotIndex >= body.slots.Length)
		{
			return;
		}

		var go = UnityEngine.Object.Instantiate((GameObject)Resources.Load(itemData.ItemId),
			body.transform.position, Quaternion.identity);
		var item = go.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			UnityEngine.Object.Destroy(go);
			_log.LogWarning("Restore: {ItemId} has no Item component — skipped.", itemData.ItemId);
			return;
		}

		item.condition = itemData.Condition;
		item.favourited = itemData.Favourited;
		RestoreLiquids(item, itemData.Liquids);
		RestoreComponentStates(item, itemData.Components);
		RestoreContents(item, itemData.Contents);

		if (body.HoldingItem(itemData.SlotIndex))
		{
			// The slot already holds something (a restored container) — the
			// item goes inside it (SaveSystem semantics, Body.cs:1388 would
			// silently refuse the slot otherwise).
			body.GetItem(itemData.SlotIndex).GetComponent<Container>()?.LoadItem(item);
		}
		else
		{
			body.PickUpItem(item, itemData.SlotIndex, force: true);
		}
	}

	private void RestoreLiquids(Item item, List<LiquidStackMsg> liquids)
	{
		var water = item.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			return;
		}

		// Rebuild the stack directly instead of AddLiquid-ing: the prefab's
		// Awake already filled the default contents (WaterContainerItem.Awake),
		// so an additive restore reads "full" again. The capture side reads the
		// same field, so this round-trips exactly (including an empty stack).
		var stackField = typeof(WaterContainerItem).GetField("stack",
			BindingFlags.NonPublic | BindingFlags.Instance);
		if (stackField is null)
		{
			_log.LogWarning("WaterContainerItem.stack field not found — liquid restore skipped (game updated?).");
			return;
		}

		stackField.SetValue(water, liquids.Select(l => new LiquidStack(l.LiquidId, l.Amount)).ToList());
	}

	private void RestoreComponentStates(Item item, List<ComponentStateMsg> states)
	{
		foreach (var state in states)
		{
			// Matched by type name: the capture side stores the component's
			// simple name, restore finds the component with that name.
			var comp = item.GetComponents<Component>()
				.FirstOrDefault(c => c.GetType().Name == state.TypeName);
			if (comp == null) // Unity object — == (FirstOrDefault on destroyed)
			{
				continue;
			}

			foreach (var field in state.Fields)
			{
				var target = comp.GetType().GetField(field.Name,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (target is null || target.IsStatic || target.IsInitOnly)
				{
					continue;
				}

				switch (field.Kind)
				{
					case 1:
						target.SetValue(comp, field.FloatValue);
						break;
					case 2:
						target.SetValue(comp, field.IntValue);
						break;
					case 3:
						target.SetValue(comp, field.BoolValue);
						break;
					case 4:
						target.SetValue(comp, field.StringValue);
						break;
					case 5:
						target.SetValue(comp, field.StringList);
						break;
				}
			}
		}
	}

	private void RestoreContents(Item containerItem, List<CharacterItemMsg> contents)
	{
		if (contents.Count == 0)
		{
			return;
		}

		var container = containerItem.GetComponent<Container>();
		if (container == null) // Unity object — ==
		{
			return;
		}

		foreach (var childData in contents)
		{
			var go = UnityEngine.Object.Instantiate((GameObject)Resources.Load(childData.ItemId),
				containerItem.transform.position, Quaternion.identity);
			var child = go.GetComponent<Item>();
			if (child == null) // Unity object — ==
			{
				UnityEngine.Object.Destroy(go);
				_log.LogWarning("Restore: {ItemId} has no Item component — skipped.", childData.ItemId);
				continue;
			}

			child.condition = childData.Condition;
			child.favourited = childData.Favourited;
			RestoreLiquids(child, childData.Liquids);
			RestoreComponentStates(child, childData.Components);
			RestoreContents(child, childData.Contents);
			container.LoadItem(child);
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
		var prevBody = _localBody; // Unity object — ==
		_localBody = inWorld ? PlayerCamera.main!.body : null;
		if (!inWorld && prevBody != null && _pendingRestore is null && !_restoreWipePending)
		{
			// Leaving the world (death, menu) — push a final snapshot so the
			// host's save carries the state at the moment of leaving, not the
			// last 1 Hz report (a death → re-enter cycle would otherwise
			// restore the pre-death state).
			_characterData.ReportCharacterData(CaptureCharacterData(prevBody));
		}

		if (inWorld && _session.Role == SessionRole.Host)
		{
			// The host entered the world: tell the members to follow (the world
			// params were published during generation, so the guest gate
			// passes). Guests already in the world ignore the instruction.
			_world.SendWorldJoin();
		}

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
		_entities.PublishLocalState(
			new NetVector2(pos.x, pos.y),
			new NetVector2(look.x, look.y),
			new NetVector2(vel.x, vel.y),
			body.isRight, body.standing, body.alive, body.conscious, body.crouching,
			sitting, body.sleeping, body.currentClimbable != null); // Unity object — ==
	}

	/// <summary>Gate for every run-start entry (StartRun/LoadRun/StartTutorial) —
	/// returns false to block. In a session a guest may only enter the world on
	/// the host's instruction (WorldJoin): starting on its own would create a
	/// world the host does not know. The WorldJoin path authorises its StartRun
	/// call right before it; LoadRun/StartTutorial have no authorised path.</summary>
	internal bool OnGuestStartAttempt()
	{
		if (_startRunAuthorized)
		{
			_startRunAuthorized = false;
			return true;
		}

		if (_session.Role == SessionRole.Guest && _session.SessionActive)
		{
			_log.LogWarning("A guest cannot start a run on its own — wait for the host to enter the world.");
			return false;
		}

		return true;
	}

	/// <summary>
	/// Guest side, in a session: the start screen (runSettingsScreen) is
	/// host-only — force it closed every frame, so the guest cannot open or
	/// operate it (the open action is wired in the scene's buttons, not in
	/// script, so closing is the reliable side). The StartRun/LoadRun/
	/// StartTutorial gates back this up.
	/// </summary>
	private void UpdateGuestMenuState()
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		var pre = PreRunScript.instance;
		if (pre == null) // Unity object — == (menu not loaded)
		{
			return;
		}

		if (pre.runSettingsScreen != null && pre.runSettingsScreen.activeSelf) // Unity object — ==
		{
			pre.runSettingsScreen.SetActive(false);
		}
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
		else if (_world.WorldParams is not null)
		{
			ApplyWorldParams(_world.WorldParams);
		}
		else
		{
			_log.LogWarning("World generation started without host world params — world will not match!");
		}
	}

	public void Dispose() => throw new NotImplementedException();
}
