using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IGameAdapter = CasualtiesUnknownOnline.Runtime.GameAdapter.IGameAdapter;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Game Adapter for the current Casualties Unknown (Demo) build (architecture.md
/// §4). The only layer that knows game types. Thin coordinator: owns the
/// lifecycle (probe/install/uninstall), the Update pump (orchestrating the
/// domain pumps) and the Runtime boundary interfaces. The Harmony patch bridge,
/// session wiring and direct player-interaction apply side live in real
/// top-level collaborators, and the deep domain logic lives in the modules
/// composed by <see cref="GameAdapterDomains"/>.
/// </summary>
public sealed class GameAdapter : IGameAdapter, ICuoService, IModEntitySpawner, IModNativeApiProvider
{
	/// <summary>
	/// Set when the game was launched via a Steam friends "Join Game"
	/// (+connect_lobby): the content-warning/intro screen is skipped so the
	/// menu is usable immediately — the follow-host pump needs PreRunScript.
	/// </summary>
	public static bool SkipIntro { get; set; }

	private readonly GameAdapterDomains _domains;
	private readonly GameAdapterBridge _bridge;
	private readonly PlayerInteractionApply _playerInteraction;
	private readonly GameAdapterSessionBinding _sessionBinding;
	private Harmony? _harmony;
	private Body? _lastLocalBody; // Unity object — == (the world-entry edge for the destroy-suppression reset)

	public GameAdapter(
		ISessionControl session,
		IEntitySyncControl entities,
		ICharacterDataControl characterData,
		IWorldControl world,
		IItemControl items,
		ICraftControl craft,
		ItemArbitration arbitration,
		EnemySyncService enemies,
		IWorldTimeControl worldTime,
		IPlayerInteractionControl playerInteraction,
		ITutorialClawControl tutorialClaw,
		IOptionsMonitor<RespawnOptions> respawnOptions,
		ILogger<GameAdapter> log,
		IMapper mapper,
		ILoggerFactory loggerFactory)
	{
		_domains = new GameAdapterDomains(session, entities, characterData, world, items, craft, arbitration,
			enemies, worldTime, playerInteraction, tutorialClaw, respawnOptions, log, mapper, loggerFactory);
		_bridge = new GameAdapterBridge(_domains);
		_playerInteraction = new PlayerInteractionApply(_domains);
		_sessionBinding = new GameAdapterSessionBinding(_domains, _playerInteraction);
		PatchBridge.Bind(_bridge); // the only static seam — Harmony patches read the narrow surface, never this instance
	}

	public string CapabilityReport { get; private set; } = "Not probed";

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
			DynamicPatchInstaller.Install(_harmony, _domains.Log);

			// Never let a failed patch silently run: verify every patch class
			// actually landed on its target (a game update that breaks a target
			// must fail loud — a silently missing hook is how sync bugs hide).
			var missing = PatchInventory.VerifyMissing(_harmony);
			if (missing.Count > 0)
			{
				_domains.Log.LogError("Game Adapter patch verification FAILED — {Count} targets not applied: {Missing}",
					missing.Count, string.Join(", ", missing));
				_harmony.UnpatchSelf();
				_harmony = null;
				return false;
			}

			_domains.Log.LogInformation("Game Adapter patches installed and verified ({Count} targets).", PatchInventory.CountTargets());
			return true;
		}
		catch (Exception ex)
		{
			_domains.Log.LogError(ex, "Game Adapter patch install failed.");
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

	void ICuoService.Initialize()
	{
		CharacterDataMapper.Configure();
		_sessionBinding.Bind();
		if (ProbeGame())
		{
			Install();
		}
		else
		{
			_domains.Log.LogError("Game Adapter probe failed — CUO multiplayer unavailable.");
		}
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		_domains.GuestMenu.Update();
		_domains.MenuInput.Update();
		_domains.Run.Update();
		_domains.WorldTimeSync.Update(); // host policy + direct-write adoption + resend; guest enforcement of the host speed

		// World-entry edge: the teardown of the PREVIOUS scene finished (its
		// destroys were suppressed, #191) — the new world's real destroys report
		// again. The edge rides the local body (null in any menu scene; Unity
		// object — ==).
		var localBody = _domains.Run.LocalBody;
		if (localBody != null && _lastLocalBody == null) // Unity objects — ==
		{
			_domains.ItemWorldSync.ResetDestroySuppression();
		}

		_lastLocalBody = localBody;
		_playerInteraction.UpdateCarriedBody(localBody); // a carried local body follows its carrier's entity state
		_domains.Gate.Update(_domains.Run.LocalBody);
		_domains.GenItemAuthority.Update(); // host/solo: publish the generation-time items when the generation finished
		_domains.GenItemApplication.Update(); // guest: apply the host's generation snapshot once the local generation finished
		_domains.Respawn.Update(); // host: next-level respawn once a world generation finishes
		_domains.TrapLayoutScanner.Update(); // host: report the generated trap layout on the same falling edge
		_domains.TrapLayoutApplication.Update(); // guest: apply a deferred layout snapshot once the local generation finished
		_domains.LayerModifierSync.Update(); // guest: apply the host's layer modifier once the local generation finished
		_domains.CarriedInventoryReporter.Update(); // guest: report the carried inventory with self-assigned ids once the local generation finished
		_domains.ItemWorldSync.FlushPendingDrop(); // a drop that was not thrown reports at end of frame (one drop = one report)
		_domains.BlockBreakSync.FlushPendingBlockBreak(); // a break's drops fold in one frame after the break — the break + drops go out as ONE message
		if (_domains.Session.Role == SessionRole.Host && _domains.Session.SessionActive)
		{
			_domains.ItemPositionAuthority.Update(); // the host's physics is the single position authority
		}
		else
		{
			_domains.ItemPositionFollow.Update(); // the guest copies simulate locally (ground-layer isolation), soft-corrected by the host's stream
		}

		_domains.WorldEventSync.Update();
		_domains.GeyserStateSync.Update(); // host/solo: capture + broadcast the geysers' liquid types once the generation finished
		_domains.RadiationLineSync.Update(); // host: publish the authoritative radiation-line state (active + timeGone)
		_domains.EntitySpawnSync.Update(); // the creation channel's deferred reports (a geyser's type, after its child Start) and carried-data applications
		_domains.FluidSync.Update(); // host: stream the members' fluid viewports (10 Hz diff + 1 Hz full)
		_domains.TradeSync.Update(); // host: the 5 s trader-state fallback broadcast
		_domains.BlockBreakSync.Update(); // expire break records without a consuming drops report
		_domains.Renderer.Update();
		_domains.EnemySync.Update(); // host: capture + publish the simulated enemies; guest: (event-driven bind/apply)
		_domains.EnemyCombat.Update(); // host: enemy combat decisions (target guidance rides the patch callbacks; bite arbitration here)
		_domains.TutorialClawSync.Update(); // host: publish the tutorial-claw presentation state (Runtime throttles the 20 Hz fan-out)
	}

	void ICuoService.Stop() => Uninstall();

	void IDisposable.Dispose()
	{
		_domains.WorldTimeSync.Unbind();
		_sessionBinding.Unbind();
		_domains.Renderer.DestroyAllClones();
		PatchBridge.Unbind(_bridge);
	}

	// ---- IGameAdapter ----

	bool IGameAdapter.IsWaitingForReady => _domains.Gate.WaitingForReady;

	bool IGameAdapter.IsInWorldOrGenerating => _domains.Run.IsInWorldOrGenerating;

	string IGameAdapter.WaitingText => _domains.Gate.WaitingText;

	void IGameAdapter.CaptureWorldParams() => _domains.WorldParams.CaptureAtBoundary();

	void IGameAdapter.ApplyWorldParams(WorldStartParams parameters) => _domains.WorldParams.Apply(parameters);

	void IGameAdapter.OnApplicationQuit() => _domains.ItemWorldSync.SuppressDestroys();

	bool IGameAdapter.HasLocalHealItem() => _playerInteraction.HasLocalHealItem();

	System.Collections.Generic.IReadOnlyList<LocalHealItem> IGameAdapter.GetLocalHealItems() => _playerInteraction.GetLocalHealItems();

	bool IGameAdapter.TryRequestTraderRecruit(ulong targetSteamId) => _domains.TraderRecruit.TryRequest(targetSteamId);

	void IGameAdapter.SetOnlineUiModal(bool visible) => _domains.MenuInput.SetModal(visible);

	// ---- Mod runtime boundaries (Phase 4 Mod API) ----

	bool IModEntitySpawner.TrySpawnEntity(string prefabId, float x, float y, float rotation) =>
		_domains.EntitySpawnSync.TrySpawnFromMod(prefabId, x, y, rotation);

	bool IModNativeApiProvider.IsRegistered(string operation) =>
		operation == ModNativeApiOperations.LocalPlayerState;

	bool IModNativeApiProvider.TryInvoke(string operation, object?[] arguments, out object? result)
	{
		result = null;

		if (operation != ModNativeApiOperations.LocalPlayerState || arguments.Length != 0)
		{
			return false;
		}

		var body = _domains.Run.LocalBody;
		if (body == null) // Unity object — == (scene-reload check)
		{
			return false;
		}

		var position = body.transform.position;
		result = new NativeLocalPlayerState(
			position.x,
			position.y,
			body.brainHealth,
			body.hunger,
			body.thirst,
			body.stamina,
			body.energy,
			body.temperature,
			body.consciousness,
			body.alive,
			body.conscious);
		return true;
	}

	private sealed class NativeLocalPlayerState(
		float x,
		float y,
		float brainHealth,
		float hunger,
		float thirst,
		float stamina,
		float energy,
		float temperature,
		float consciousness,
		bool alive,
		bool conscious) : IModNativeLocalPlayerState
	{
		public float X { get; } = x;

		public float Y { get; } = y;

		public float BrainHealth { get; } = brainHealth;

		public float Hunger { get; } = hunger;

		public float Thirst { get; } = thirst;

		public float Stamina { get; } = stamina;

		public float Energy { get; } = energy;

		public float Temperature { get; } = temperature;

		public float Consciousness { get; } = consciousness;

		public bool Alive { get; } = alive;

		public bool Conscious { get; } = conscious;
	}
}
