using System;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.WorldGen;
using Microsoft.Extensions.Logging;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The world-start parameters domain: capture the generation baseline (RNG
/// state + run settings + world-defining fields) at the run-start entry or the
/// GenerateWorld boundary, publish it for the guests, restore it on the guest
/// (so both sides generate the identical world) and force the generation
/// stream back to it right before the coroutine starts. Split out of
/// RunCoordinator (gate-driven — it was 598 lines, 2 from the 600-line gate);
/// the run-lifecycle phase machine stays in RunCoordinator, which calls the
/// two boundary hooks (OnGenerateBoundary host branch / EnsureGuestApplied
/// guest branch).
///
/// It depends on the <see cref="INativeWorldFacts"/> PORT, never on the concrete
/// <c>NativeWorldFacts</c>: every call it makes (cancel a superseded handover, apply
/// the baseline's rarity multipliers) is part of that contract, so the service can
/// be composed and driven against any implementation of it instead of being welded
/// to the adapter's own.
/// </summary>
internal sealed class WorldParamsService(
	IWorldControl world,
	INativeWorldFacts nativeWorldFacts,
	ILogger<WorldParamsService> log)
{
	private readonly IWorldControl _world = world;
	private readonly INativeWorldFacts _nativeWorldFacts = nativeWorldFacts;
	private readonly ILogger<WorldParamsService> _log = log;

	/// <summary>Host: params captured at the run-start entry — the first GenerateWorld must not re-capture.</summary>
	private bool _entryParamsCaptured;

	/// <summary>Host: a CUO world restore owns the next generation boundary — replay the RESTORED baseline, never capture a new one.</summary>
	private bool _restorePending;

	/// <summary>Guest: the params instance whose Random.state is currently restored (a new instance = a new world/layer = re-apply).</summary>
	private WorldStartParams? _appliedWorldParams;

	/// <summary>Guest: the "generation holding for params" log fired for this wait.</summary>
	private bool _guestParamsWaitLogged;

	/// <summary>
	/// Host: capture + publish the world params at the run-start entry (the
	/// click moment), BEFORE any run randomness is consumed — the generation
	/// stream is force-reset to this baseline at the GenerateWorld boundary, so
	/// capturing now is equivalent to capturing there, and the guests get the
	/// params with zero waiting. Run settings come from the menu (PreRunScript —
	/// WorldGeneration.runSettings is only assigned inside StartRun); the
	/// tutorial nulls them itself (PreRunScript.cs:312). The world-defining
	/// fields are all defaults at the entry: biomeOverride follows the entry
	/// kind (tutorial or not — its other source is the WorldGeneration.Awake
	/// tutorial flag, identical on both sides), depth and traveled start at 0
	/// (debugStartDepth is a debug-console value).
	/// </summary>
	internal void CaptureAtEntry(bool isTutorial)
	{
		_world.ResetDamagedBlocks(); // the new run's damage table starts empty again
									 // The layer this handover belonged to is gone: a native restore still waiting
									 // for a world-entry seam that will never come must NOT be replayed into the
									 // world this new run generates (the restore path bypasses this method, so an
									 // armed restore is never cancelled here by mistake).
		_nativeWorldFacts.CancelPendingRestore();

		var randomState = RandomStateSerializer.Serialize(Random.state);
		var runSettings = isTutorial ? null : HarmonyTraverse.ReadPreRunRunSettings();
		_world.PublishWorldParams(new WorldStartParams
		{
			RandomState = randomState,
			RunSettings = runSettings,
			BiomeOverride = isTutorial ? (byte)WorldGeneration.OverrideSceneType.Tutorial : (byte)WorldGeneration.OverrideSceneType.None,
			BiomeDepth = 0,
			TotalTraveled = 0,
			// The debug console's starting depth is READ, not assumed: it is the third clause
			// of the game's own first-layer test (`WorldGeneration.cs:1891`), so a run the
			// player started at a debug depth must not read as the run's first layer.
			DebugStartDepth = (byte)HarmonyTraverse.ReadDebugStartDepth(),
		});
		_entryParamsCaptured = true;
		_log.LogInformation("Captured world params at run-start entry ({StateBytes} bytes, {SettingCount} settings, tutorial: {Tutorial}).",
			randomState.Length, runSettings?.Count ?? 0, isTutorial);
	}

	/// <summary>
	/// Host, at the GenerateWorld boundary. First generation of a run that
	/// captured its params at the click moment: the entry capture is consumed
	/// (re-capturing here would move the baseline and re-send, racing the
	/// guests' already-started runs). Otherwise (layer switch, solo, load-run):
	/// snapshot what defines a run before generation consumes the RNG — the
	/// world-defining fields (biome override/depth, total traveled) were dead
	/// on the wire until this step, now captured with the RNG state.
	/// </summary>
	internal void OnGenerateBoundary()
	{
		if (_restorePending)
		{
			_restorePending = false;
			ApplyRestoredBaseline();
			return;
		}

		if (_entryParamsCaptured)
		{
			_entryParamsCaptured = false;
			return;
		}

		CaptureAtBoundary();
	}

	/// <summary>
	/// Host: the Continue entry restored a CUO world — the next generation boundary
	/// must replay the SAVED baseline (the kernel restore projected it into
	/// <see cref="IWorldControl.WorldParams"/>) instead of capturing the live RNG.
	/// Capturing here would generate a layer other than the one the snapshot
	/// stores, which is exactly the silent "regenerate the layer" the restore
	/// contract forbids.
	/// </summary>
	internal void MarkRestorePending() => _restorePending = true;

	/// <summary>A new run owns the next generation: a restore armed for a previous attempt must never replay into it.</summary>
	internal void CancelRestorePending() => _restorePending = false;

	/// <summary>
	/// Host: apply the restored baseline NOW. The Continue click happens before the
	/// scene load, and <c>WorldGeneration.Start</c> derives fields from
	/// <c>WorldGeneration.runSettings</c> (unchipped, the rarity multipliers, the time
	/// limit, decay rate, temperature offset, liquid pushing, debug chunk sizes) BEFORE
	/// GenerateWorld ever fires — applying at the boundary alone would let the new layer
	/// start from the live menu settings instead of the saved ones. Returns false when
	/// the restore published no baseline: that is a refusal, never a silent fallback.
	/// </summary>
	internal bool TryApplyRestoredNow()
	{
		var parameters = _world.WorldParams;
		if (parameters is null)
		{
			_log.LogError("The restore published no world params — the layer cannot be reproduced from the snapshot.");
			return false;
		}

		Apply(parameters);
		_restorePending = true; // the boundary re-applies it, idempotently, after the scene swap
		_log.LogInformation("Applied the RESTORED run baseline at the continue click ({StateBytes} RNG bytes, depth {Depth}).",
			parameters.RandomState.Length, parameters.BiomeDepth);
		return true;
	}

	private void ApplyRestoredBaseline()
	{
		var parameters = _world.WorldParams;
		if (parameters is null)
		{
			_log.LogError("The restore published no world params; the layer cannot be reproduced from the snapshot — generation continues from the live RNG stream.");
			return;
		}

		// The guest side's application path (proven by the two-side generation
		// match): world-defining fields + run settings, then the RNG state, which
		// the generation wrapper re-forces at the coroutine start.
		Apply(parameters);
		_log.LogInformation("Applied the RESTORED run baseline (depth {Depth}, override {Override}, traveled {Traveled}, {StateBytes} RNG bytes).",
			parameters.BiomeDepth, parameters.BiomeOverride, parameters.TotalTraveled, parameters.RandomState.Length);
	}

	/// <summary>Host side: capture + publish the world params at the GenerateWorld boundary (layer switches, solo, load-run — the entry capture does not apply).</summary>
	internal void CaptureAtBoundary()
	{
		// Host side: a new world (or layer) is generating — the damage table
		// starts empty again; mutations during generation are the baseline.
		_world.ResetDamagedBlocks();
		// Same rule as the entry capture: this boundary replaces the layer the
		// waiting native values belong to (the restore path bypasses this method, so
		// an armed restore is never cancelled here by mistake).
		_nativeWorldFacts.CancelPendingRestore();

		var randomState = RandomStateSerializer.Serialize(Random.state);
		var runSettings = HarmonyTraverse.ReadRunSettings();
		var biomeOverride = (byte)HarmonyTraverse.ReadBiomeOverride();
		var biomeDepth = (byte)HarmonyTraverse.ReadBiomeDepth();
		var totalTraveled = HarmonyTraverse.ReadTotalTraveled();
		var debugStartDepth = (byte)HarmonyTraverse.ReadDebugStartDepth();

		// The rarity multipliers are world-defining inputs like the fields above:
		// the layer's loot/trap distribution is scaled by them, and the game has
		// already applied this layer's accumulation by the time this boundary runs
		// (WorldGeneration.cs:1061-1062, before InstantiateWorld). A side that
		// generated with the game's fresh 1f would build a different layer than this
		// one, so they travel with the baseline. They are read from the LIVE world,
		// never from the run settings, which only carry the per-layer increments.
		var world = WorldGeneration.world;
		float? lootRarity = null;
		float? trapRarity = null;
		if (world != null) // Unity object — ==
		{
			lootRarity = world.lootRarityMultiplier;
			trapRarity = world.trapRarityMultiplier;
		}
		else
		{
			_log.LogWarning("World params captured with no live world: the rarity multipliers are not part of this baseline, and a peer generating from it would use the game's fresh values.");
		}

		_world.PublishWorldParams(new WorldStartParams
		{
			RandomState = randomState,
			RunSettings = runSettings,
			BiomeOverride = biomeOverride,
			BiomeDepth = biomeDepth,
			TotalTraveled = totalTraveled,
			DebugStartDepth = debugStartDepth,
			LootRarityMultiplier = lootRarity,
			TrapRarityMultiplier = trapRarity,
			// LoadedRun: no backing game field (PreRunScript.LoadRun is the
			// save-load flow — Phase 3 saves scope) — stays false on the wire.
		});
		_log.LogInformation("Captured world params ({StateBytes} bytes, {SettingCount} settings, "
			+ "biome {Biome}/{Depth}, traveled {Traveled}, loot {Loot}, trap {Trap}).",
			randomState.Length, runSettings?.Count ?? 0, biomeOverride, biomeDepth, totalTraveled,
			lootRarity?.ToString("F3") ?? "<none>", trapRarity?.ToString("F3") ?? "<none>");
	}

	/// <summary>
	/// Guest side, called before the generation coroutine may consume any
	/// Random: false while the host's world params have not arrived (the
	/// wrapper holds the coroutine — nothing random consumed yet); on arrival
	/// restores them and returns true. Idempotent per params instance — a layer
	/// switch delivers a new instance and re-applies. Host/solo: nothing to
	/// wait for.
	/// </summary>
	internal bool EnsureGuestApplied()
	{
		var parameters = _world.WorldParams;
		if (parameters is null)
		{
			// The wrapper polls every frame — log the hold once per wait, so a
			// held generation is observable without spamming the log.
			if (!_guestParamsWaitLogged)
			{
				_guestParamsWaitLogged = true;
				_log.LogInformation("World generation holding — host world params not arrived yet (fast guest transition).");
			}

			return false;
		}

		_guestParamsWaitLogged = false; // re-arm for the next world/layer
		if (!ReferenceEquals(_appliedWorldParams, parameters))
		{
			Apply(parameters);
			_appliedWorldParams = parameters;
		}

		return true;
	}

	/// <summary>
	/// Session ended: every capture/apply marker is session-scoped. The next
	/// host run captures new params; the next guest follow applies the new
	/// host's params (reference identity distinguishes them, but a dead
	/// marker must never consume the next run's capture).
	/// </summary>
	internal void ResetForSessionEnd()
	{
		_entryParamsCaptured = false;
		_restorePending = false;
		_appliedWorldParams = null;
		_guestParamsWaitLogged = false;
	}

	/// <summary>Guest side: restore the host's RNG state + run settings + world-defining fields so local world generation produces the same world. A new params instance IS a new world/layer, so the guest's unacknowledged block reports (state and partial damage alike) from the previous world are dropped here — the host reset its own difference table at the same generation boundary.</summary>
	internal void Apply(WorldStartParams parameters)
	{
		_world.ResetPendingBlockReports();
		_world.ResetPendingBlockDamageReports();
		_world.ResetPendingEntityReports();

		Random.state = RandomStateSerializer.Deserialize(parameters.RandomState);
		if (parameters.RunSettings is not null)
		{
			HarmonyTraverse.WriteRunSettings(parameters.RunSettings);
		}

		HarmonyTraverse.WriteBiomeOverride(parameters.BiomeOverride);
		HarmonyTraverse.WriteBiomeDepth(parameters.BiomeDepth);
		HarmonyTraverse.WriteTotalTraveled(parameters.TotalTraveled);

		// DebugStartDepth is deliberately NOT written back: the game never writes that field
		// (`WorldGeneration.cs:4258` is its declaration and `:247`/`:257`/`:1891` are its only
		// readers), so it is a scene-serialized constant, and the kernel baseline does not carry it
		// between peers — writing the local default here would only mask the value the game will
		// actually use. The clause it belongs to is the HOST's to evaluate (see
		// StartingSupplyPolicy.NativeGrantCovers).

		// The generation boundary's rarity multipliers: the host captured them from
		// the world it just generated with, and the guest must generate the SAME
		// layer. An older sender that carries none leaves the game's own Start-time
		// value in place, which is the behavior the sender itself had. A sender that
		// carries only ONE of the two is not guessed at: the pair is captured together,
		// so a half-pair is a producer bug and the other half keeps the game's value.
		// A value the game could not have produced — a NaN or an infinity — is refused
		// exactly like the half-pair below: NEITHER multiplier is written, so the layer
		// keeps the game's own values instead of being scaled by a number that has no
		// meaning. The kernel refuses such a run baseline too (RunRarityMultipliers'
		// rule, asserted by WorldDomainModule), but this is the write into the LIVE
		// world, and the params object read here is the one the publisher stored — not
		// the kernel's projection — so the last line sits at this seam as well.
		if (!RunRarityMultipliers.IsWellFormed(parameters.LootRarityMultiplier) || !RunRarityMultipliers.IsWellFormed(parameters.TrapRarityMultiplier))
		{
			_log.LogError(
				"[WorldParams] the run baseline carries a non-finite rarity multiplier (loot {Loot}, trap {Trap}); neither is applied, so the layer keeps the game's own values and does NOT match the run's captured distribution.",
				parameters.LootRarityMultiplier?.ToString("F3") ?? "<none>",
				parameters.TrapRarityMultiplier?.ToString("F3") ?? "<none>");
		}
		else if (parameters.LootRarityMultiplier is not null && parameters.TrapRarityMultiplier is not null)
		{
			try
			{
				_nativeWorldFacts.ApplyRunGenerationMultipliers(parameters.LootRarityMultiplier.Value, parameters.TrapRarityMultiplier.Value);
			}
			catch (Exception ex)
			{
				// The adapter is the only layer that can throw here (it touches the live
				// game), and this runs inside the generation coroutine: a throw must not
				// abort world generation over a multiplier, so it is named and the game's
				// own value stands.
				_log.LogError(ex, "[WorldParams] the live world refused the run baseline's rarity multipliers (loot {Loot}, trap {Trap}); the layer keeps the game's own values.",
					parameters.LootRarityMultiplier.Value, parameters.TrapRarityMultiplier.Value);
			}
		}
		else if (parameters.LootRarityMultiplier is not null || parameters.TrapRarityMultiplier is not null)
		{
			_log.LogWarning(
				"[WorldParams] the run baseline carries only one rarity multiplier (loot {Loot}, trap {Trap}); neither is applied, so the layer keeps the game's own values.",
				parameters.LootRarityMultiplier?.ToString() ?? "<none>", parameters.TrapRarityMultiplier?.ToString() ?? "<none>");
		}

		_log.LogInformation("Applied host world params ({StateBytes} bytes, loot {Loot}, trap {Trap}).",
			parameters.RandomState.Length,
			parameters.LootRarityMultiplier?.ToString("F3") ?? "<none>",
			parameters.TrapRarityMultiplier?.ToString("F3") ?? "<none>");
	}

	/// <summary>
	/// Both sides: force Random.state back to the captured baseline right before
	/// the generation coroutine starts. The host captured it at its run-start
	/// entry — everything consumed between that moment and here (transition,
	/// scene loading, WorldGeneration.Start) is overwritten, keeping the two
	/// generation streams identical. Guest: the params were just applied by
	/// <see cref="EnsureGuestApplied"/> — same value, idempotent.
	/// </summary>
	internal void ResetGenStreamToBaseline()
	{
		var parameters = _world.WorldParams;
		if (parameters is null)
		{
			return;
		}

		Random.state = RandomStateSerializer.Deserialize(parameters.RandomState);
		_log.LogInformation("Generation stream reset to captured baseline ({StateBytes} bytes: {StateHex}).",
			parameters.RandomState.Length, BitConverter.ToString(parameters.RandomState).Replace("-", ""));
	}
}
