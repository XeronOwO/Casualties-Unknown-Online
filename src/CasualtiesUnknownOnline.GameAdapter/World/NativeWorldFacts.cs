using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The Game Adapter's <see cref="INativeWorldFacts"/>: the world values only the
/// game can read and write — keypad codes, geyser liquid types and the game's own
/// partial block damage (<c>WorldGeneration.world.blockDamages</c>) — plus the
/// values that shape the RUN rather than the layer: the run clock base and the
/// recipe unlock table.
///
/// The capture half runs while a world is alive and reads the live tables. The
/// apply half CANNOT write at the Continue click (the world does not exist yet),
/// so it holds the restored values until a seam consumes them:
/// <see cref="RestoredWorldFactReplay"/> takes the layer facts once the generation
/// completed; the run fields go through
/// <see cref="TryWritePendingRunFields"/>, which the native save slot calls before
/// <c>WorldGeneration.Start</c> derives the layer's time limit and trap budget
/// from them.
///
/// A pending restore that is never consumed is NOT left behind: the run that owns
/// it calls <see cref="CancelPendingRestore"/> (a new run through the save layer's
/// <c>TryBeginRun</c>, or the end of the session), so the values can never land in
/// a different world. Holding them instead of writing early is deliberate: a
/// keypad code belongs to an <c>Openable</c> the generation has not created yet,
/// and a partial-damage row belongs to a cell whose block does not exist yet.
/// Writing earlier would be a silent no-op that the restore report would call a
/// success.
///
/// Public because the plugin's composition root is the layer that registers the
/// port for the Runtime's save service (the adapter is the only layer that can
/// implement it, and the Runtime cannot reference the adapter).
/// </summary>
public sealed class NativeWorldFacts(ILogger<NativeWorldFacts> log) : INativeWorldFacts
{
	private List<KeypadEntryMsg>? _pendingKeypads;
	private List<GeyserStateEntryMsg>? _pendingGeysers;
	private List<BlockDamageEntryMsg>? _pendingBlockDamages;

	/// <summary>
	/// The restored recipe unlock table. It waits for the WORLD-ENTRY seam like the
	/// three above, and unlike the run clock: the game rebuilds
	/// <c>Recipes.recipes</c> in <c>WorldGeneration.Awake</c> and CUO's mod-content
	/// provider appends the custom recipes on a LATER Update frame
	/// (<c>GameAdapterRecipeContentProvider</c>), so a write at the native save slot
	/// would refuse every custom recipe's row.
	/// </summary>
	private List<SaveRecipeUnlockRow>? _pendingRecipes;

	/// <summary>The run baseline's generation-boundary rarity multipliers, held until a live world can take them.</summary>
	private float? _pendingGenerationLoot;
	private float? _pendingGenerationTrap;

	/// <summary>The restored cut's run clock base, held until a live world can take it.</summary>
	private float? _pendingRunTime;

	/// <summary>
	/// The run clock base handed over by the world-entry message (the host read it off
	/// its own live world), held until a live world can take it.
	/// </summary>
	private float? _pendingClock;

	/// <summary>
	/// True once a clock value has been written into the CURRENT world. It is what
	/// makes the write-once rule per world rather than per process: a new layer (or a
	/// fresh scene) must take the generation boundary's new clock base, while a
	/// duplicate of the value already written in this world must not touch a clock
	/// that has been running since.
	/// </summary>
	private bool _sawRunClock;

	/// <summary>The layer timer values handed over by the world-entry message, held until the live world can take them.</summary>
	private float? _pendingLayerTimeSpent;

	/// <summary>The layer LIMIT handed over with them, held for the window before the game derives its own from the run settings.</summary>
	private float? _pendingMaxTimePerLayer;

	/// <inheritdoc />
	public bool HasPendingRestore =>
		_pendingKeypads is not null || _pendingGeysers is not null || _pendingBlockDamages is not null
		|| _pendingRecipes is not null;

	/// <inheritdoc />
	public NativeWorldFactCapture Capture()
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — == (a layer-end cut never reads these; a mid-run cut always has one)
		{
			// NOT an empty table: the game's own partial-damage list is the only
			// table that holds it, so reading "no world" as "no damage" would store
			// a clean world and lose every crack. The failure refuses the cut.
			log.LogError("[SaveFacts] no live world to read the native world tables from — the cut must not store an empty damage list.");
			return NativeWorldFactCapture.Unreadable("no live world is present, so the game's own block-damage table could not be read");
		}

		return new NativeWorldFactCapture(
			KeypadCodeTable.Capture(),
			GeyserStateTable.Capture(),
			GameBlockDamageTable.Capture(world),
			Failure: null);
	}

	/// <inheritdoc />
	public IReadOnlyList<BlockDamageEntryMsg>? CaptureBlockDamages()
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — == (the caller sends nothing and says so)
		{
			return null;
		}

		return GameBlockDamageTable.Capture(world);
	}

	/// <inheritdoc />
	public NativeRunFields CaptureRunFields()
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			// A zero run clock and an empty recipe table are REAL values a restore
			// would write onto the world, so "no world" must not come back as one.
			log.LogError("[SaveFacts] no live world to read the native run fields from — the cut must not store a zero run clock and a re-locked recipe table.");
			return NativeRunFields.Unreadable("no live world is present, so the run clock base and the recipe unlock table could not be read");
		}

		var recipes = RecipeUnlockTable.Capture();
		if (recipes is null)
		{
			log.LogError("[SaveFacts] the live recipe table is not built — the cut must not store an empty unlock table.");
			return NativeRunFields.Unreadable("the live recipe table is not built, so the recipe unlock state could not be read");
		}

		// The game's own save values (SaveSystem.cs:165, :178-179): the accumulated
		// clock base plus the layer time that has passed since, and the two rarity
		// multipliers as generation left them. Reading only savedRunTime would
		// rewind the clock by the whole layer; reading only realTimeElapsed would
		// drop every earlier layer.
		//
		// The LAYER timer is read at the same instant and travels with the same cut,
		// but it is not a run value: the restore writes it back so a continued layer
		// resumes the radiation line's countdown instead of restarting it (the
		// native continue restarts it — its own save carries no such value and
		// WorldGeneration zeroes layerTimeSpent when the layer finishes generating (WorldGeneration.cs:3609, in FinishWorldGeneration)). Its LIMIT is not recorded:
		// the game recomputes maxTimePerLayer from the restored run settings.
		return new NativeRunFields(
			world.lootRarityMultiplier,
			world.trapRarityMultiplier,
			SaveSystem.savedRunTime + world.realTimeElapsed,
			recipes,
			Failure: null,
			LayerTimeSpent: world.layerTimeSpent);
	}

	/// <inheritdoc />
	public RunClockFacts CaptureRunClockFacts()
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			// Not a failure of a restore: the callers are the generation boundary and
			// the world-entry edge, and the value a receiver would take from "no
			// world" is a zero nobody read.
			return RunClockFacts.Unreadable;
		}

		return new RunClockFacts(
			SaveSystem.savedRunTime + world.realTimeElapsed,
			world.layerTimeSpent,
			world.maxTimePerLayer,
			Failure: null);
	}

	/// <inheritdoc />
	public void ApplyRunFacts(RunClockFacts facts)
	{
		if (facts.Failure is not null)
		{
			// An unreadable capture is the ABSENCE of the value, and a receiver keeps
			// its own clock and timer — writing 0 there would restart both.
			log.LogWarning("[RunFacts] the peer's run clock facts could not be read ({Failure}); this side keeps its own clock and layer timer.", facts.Failure);
			return;
		}

		_pendingClock = facts.RunClockBase;
		_pendingLayerTimeSpent = facts.LayerTimeSpent;
		_pendingMaxTimePerLayer = facts.MaxTimePerLayer;
		if (WritePendingClockFacts())
		{
			return;
		}

		log.LogInformation(
			"[RunFacts] holding the run clock base {Clock:F1}s and the layer timer {Spent:F1}/{Limit:F1}s until the live world can take them.",
			facts.RunClockBase, facts.LayerTimeSpent, facts.MaxTimePerLayer);
	}

	/// <inheritdoc />
	public void SettleRunClockFacts()
	{
		// A generation boundary is the one place the per-world clock marker must be
		// re-armed: the world that just ended took its clock, and the world this
		// boundary is starting has to take the new base. Any value still waiting is
		// dropped rather than written, because it was captured in (or sent for) the
		// world that is being replaced.
		_sawRunClock = false;
		ClearPendingClockFacts();
	}

	/// <inheritdoc />
	public IReadOnlyList<int>? CaptureUnlockedRecipeIndexes()
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			// No live world. `Recipes.recipes` OUTLIVES a run (the game rebuilds it
			// in `WorldGeneration.Awake`), so reading it here could report the
			// PREVIOUS run's unlocks: refuse instead, and both halves retry on the
			// next cycle rather than inheriting a dead run's set.
			log.LogDebug("[Crafting] no live world — the recipe-unlock set is not read.");
			return null;
		}

		var unlocked = RecipeUnlockTable.CaptureUnlockedIndexes();
		if (unlocked is null)
		{
			log.LogDebug("[Crafting] the live recipe table is not built — the recipe-unlock set is not read.");
			return null;
		}

		return unlocked;
	}

	/// <inheritdoc />
	public void ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes) => _pendingKeypads = [.. codes];

	/// <inheritdoc />
	public void ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers) => _pendingGeysers = [.. geysers];

	/// <inheritdoc />
	public void ApplyBlockDamages(IReadOnlyList<BlockDamageEntryMsg> damages) => _pendingBlockDamages = [.. damages];

	/// <inheritdoc />
	public void ApplyCutRunFields(float savedRunTime, float? layerTimeSpent)
	{
		_pendingRunTime = savedRunTime;
		if (layerTimeSpent is { } spent)
		{
			// The layer's own countdown resumes where the cut left it, instead of the native
			// continue's restart (the game zeroes layerTimeSpent when a layer finishes generating — WorldGeneration.cs:3609 — and the native
			// save carries no value to put back).
			_pendingLayerTimeSpent = spent;
		}

		if (!WriteRunFieldsIfPossible() || !WritePendingClockFacts())
		{
			log.LogInformation(
				"[SaveFacts] holding the restored run clock base ({RunTime:F1}s) and the layer timer ({LayerTime}) until the live world can take them.",
				savedRunTime, layerTimeSpent?.ToString("F1") ?? "<none>");
		}
	}

	/// <inheritdoc />
	public void ApplyRecipeUnlocks(IReadOnlyList<SaveRecipeUnlockRow> recipes) => _pendingRecipes = [.. recipes];

	/// <inheritdoc />
	public bool ApplyRunGenerationMultipliers(float lootRarityMultiplier, float trapRarityMultiplier)
	{
		_pendingGenerationLoot = lootRarityMultiplier;
		_pendingGenerationTrap = trapRarityMultiplier;
		if (WriteRunFieldsIfPossible())
		{
			return true;
		}

		log.LogInformation(
			"[SaveFacts] holding the run baseline's rarity multipliers (loot {Loot}, trap {Trap}) until the live world exists.",
			lootRarityMultiplier, trapRarityMultiplier);
		return false;
	}

	/// <inheritdoc />
	public bool TryWritePendingRunFields()
	{
		if (!HasPendingRunFields && !HasPendingClockFacts)
		{
			return true;
		}

		if (WorldGeneration.world == null) // Unity object — ==
		{
			// Not a failure of the restore: the values stay pending for the next
			// seam (the world-entry edge). The caller must not treat this as written.
			return false;
		}

		WriteRunFieldsIfPossible();
		WritePendingClockFacts();
		return true;
	}

	/// <inheritdoc />
	public NativeWorldFactRestore ReadPendingRestore()
	{
		if (!HasPendingRestore)
		{
			return NativeWorldFactRestore.Empty;
		}

		return new NativeWorldFactRestore(
			_pendingKeypads ?? [],
			_pendingGeysers ?? [],
			_pendingBlockDamages ?? [],
			_pendingRecipes ?? []);
	}

	/// <inheritdoc />
	public void CommitPendingRestore()
	{
		if (!HasPendingRestore)
		{
			return;
		}

		log.LogInformation(
			"[SaveFacts] committed the restored native world facts ({Keypads} keypad code(s), {Geysers} geyser type(s), {Damages} game block-damage row(s), {Recipes} recipe unlock row(s)): the live world has them.",
			_pendingKeypads?.Count ?? 0, _pendingGeysers?.Count ?? 0, _pendingBlockDamages?.Count ?? 0, _pendingRecipes?.Count ?? 0);
		ClearPending();
	}

	/// <inheritdoc />
	public void CancelPendingRestore()
	{
		if (!HasPendingRestore && !HasPendingRunFields)
		{
			return;
		}

		log.LogInformation(
			"[SaveFacts] cancelled the pending restored native values — world facts ({Keypads} keypad code(s), {Geysers} geyser type(s), {Damages} game block-damage row(s), {Recipes} recipe unlock row(s)) and run fields ({RunFields}): the run that owned them never reached the world-entry seam.",
			_pendingKeypads?.Count ?? 0, _pendingGeysers?.Count ?? 0, _pendingBlockDamages?.Count ?? 0, _pendingRecipes?.Count ?? 0, DescribePendingRunFields());
		ClearPending();
		ClearPendingRunFields();
	}

	/// <summary>True = a restored run value (rarity multipliers, run clock) is still waiting for the live world.</summary>
	private bool HasPendingRunFields =>
		_pendingGenerationLoot is not null || _pendingGenerationTrap is not null || _pendingRunTime is not null;

	/// <summary>
	/// Writes every waiting run value onto the live world, if there is one. True =
	/// nothing is pending any more. Each value is written only when it is actually
	/// pending, so a boundary that has nothing to restore never overwrites what the
	/// game put there.
	///
	/// The recipe unlock table is deliberately NOT here: it needs the world's
	/// COMPLETE recipe table, which only exists after CUO's mod-content provider has
	/// appended the custom recipes on an Update frame — i.e. at the world-entry seam,
	/// where <see cref="ReadPendingRestore"/> hands it to the replay.
	/// </summary>
	private bool WriteRunFieldsIfPossible()
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return false;
		}

		var loot = _pendingGenerationLoot;
		var trap = _pendingGenerationTrap;
		var runTime = _pendingRunTime;

		if (loot is not null)
		{
			world.lootRarityMultiplier = loot.Value;
		}

		if (trap is not null)
		{
			world.trapRarityMultiplier = trap.Value;
		}

		if (runTime is not null)
		{
			// The native save slot's own assignment (SaveSystem.cs:439): the stored
			// value IS the new clock base, because the game wrote base + elapsed.
			SaveSystem.savedRunTime = runTime.Value;
		}

		log.LogInformation(
			"[SaveFacts] wrote the run values into the live world: loot {Loot}, trap {Trap}, clock base {RunTime}.",
			loot is null ? "<unchanged>" : loot.Value.ToString("F3"),
			trap is null ? "<unchanged>" : trap.Value.ToString("F3"),
			runTime is null ? "<unchanged>" : runTime.Value.ToString("F1"));
		ClearPendingRunFields();
		return true;
	}

	private string DescribePendingRunFields() =>
		$"loot {(_pendingGenerationLoot?.ToString("F3") ?? "-")}, "
		+ $"trap {(_pendingGenerationTrap?.ToString("F3") ?? "-")}, "
		+ $"clock {(_pendingRunTime?.ToString("F1") ?? "-")}";

	/// <summary>True = a run clock base or a layer timer is waiting for the live world.</summary>
	private bool HasPendingClockFacts =>
		_pendingClock is not null || _pendingLayerTimeSpent is not null || _pendingMaxTimePerLayer is not null;

	/// <summary>
	/// Writes the waiting run-clock values into the live world, if there is one. True =
	/// nothing is waiting any more.
	///
	/// The CLOCK is written at most once per world: the value a generation boundary read
	/// is the base that boundary's new scene derives from, so the first write is the
	/// correct one, and a duplicate of it (the 60 s repair group re-sends the same
	/// absolute value) must not overwrite a clock that has been running since — that is
	/// the one write here that could move the run clock BACKWARDS.
	///
	/// The LAYER TIMER is written as "this layer had already spent at least this long":
	/// a smaller value could only come from a message that left the host before this
	/// side's layer began, and moving the timer backwards would push the radiation line's
	/// activation away.
	/// </summary>
	private bool WritePendingClockFacts()
	{
		if (!HasPendingClockFacts)
		{
			return true;
		}

		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return false;
		}

		var clock = _pendingClock;
		var layerTime = _pendingLayerTimeSpent;
		ApplyLiveClockFacts(new RunClockFacts(
			clock ?? 0f,
			layerTime ?? -1f,
			_pendingMaxTimePerLayer ?? -1f,
			Failure: null));
		_pendingClock = null;
		_pendingLayerTimeSpent = null;
		_pendingMaxTimePerLayer = null;
		return true;
	}

	/// <summary>
	/// The one place the live world takes a run clock or a layer timer. Both are matched
	/// against what this process already holds, so no reachable path can move either
	/// backwards: <see cref="_sawRunClock"/> is what lets a NEW world take a clock that is
	/// lower than the previous world's final value, because a new layer's clock base is
	/// captured at the boundary and legitimately sits below the total the last frame of the
	/// previous layer held. That arm is re-armed on the HOST only, at the generation
	/// boundary (<see cref="SettleRunClockFacts"/>); a guest never re-arms it, and does not
	/// need to — it only ever receives the host's increasing total.
	///
	/// The layer LIMIT is filled in only when the game has none: <c>WorldGeneration.Start</c>
	/// derives it from the run settings, so a value already there IS that derivation and
	/// outranks a copied one — this write exists for the window before the game's own
	/// derivation has run.
	/// </summary>
	private void ApplyLiveClockFacts(RunClockFacts facts)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		var clockWritten = false;
		if (facts.RunClockBase > 0f && (!_sawRunClock || facts.RunClockBase > SaveSystem.savedRunTime))
		{
			// The native save slot's own assignment (SaveSystem.cs:439): the stored value
			// IS the new clock base, because the game wrote base + elapsed. It is written
			// before WorldGeneration.Start derives the layer's time limit from it.
			SaveSystem.savedRunTime = facts.RunClockBase;
			_sawRunClock = true;
			clockWritten = true;
		}

		var layerTimeWritten = false;
		if (facts.LayerTimeSpent > world.layerTimeSpent)
		{
			world.layerTimeSpent = facts.LayerTimeSpent;
			layerTimeWritten = true;
		}

		// The LIMIT is filled in only when the game has none: WorldGeneration.Start
		// derives it from the run settings, so a value already there IS that
		// derivation and outranks a copied one — this write exists for the window
		// before the game's own derivation has run.
		var limitWritten = false;
		if (facts.MaxTimePerLayer > 0f && world.maxTimePerLayer <= 0f)
		{
			world.maxTimePerLayer = facts.MaxTimePerLayer;
			limitWritten = true;
		}

		if (clockWritten || layerTimeWritten || limitWritten)
		{
			log.LogInformation(
				"[RunFacts] the live world took the run clock base {Clock:F1}s ({ClockState}), the layer timer {Spent:F1}s ({TimerState}) and the layer limit {Limit:F1}s ({LimitState}).",
				SaveSystem.savedRunTime, clockWritten ? "written" : "kept — this world has already taken one and the value does not advance it",
				world.layerTimeSpent, layerTimeWritten ? "written" : "kept — the layer has already spent at least as long",
				world.maxTimePerLayer, limitWritten ? "written" : "kept — the game's own derivation already set it");
		}
	}

	private void ClearPendingClockFacts()
	{
		_pendingClock = null;
		_pendingLayerTimeSpent = null;
		_pendingMaxTimePerLayer = null;
	}

	/// <summary>The pending set is done with — taken by the replay, or discarded when a new run or the session end supersedes it.</summary>
	private void ClearPending()
	{
		_pendingKeypads = null;
		_pendingGeysers = null;
		_pendingBlockDamages = null;
		_pendingRecipes = null;
	}

	private void ClearPendingRunFields()
	{
		_pendingGenerationLoot = null;
		_pendingGenerationTrap = null;
		_pendingRunTime = null;
		// A world that has just been superseded must take the next clock it is handed:
		// the marker is per world, and this is where "this world" ends.
		_sawRunClock = false;
	}
}
