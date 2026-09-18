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
	public IReadOnlyList<BlockDamageEntryMsg>? MergeBlockDamages(IReadOnlyList<BlockDamageEntryMsg> reported)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — == (the caller answers nothing and says so)
		{
			log.LogWarning(
				"[BlockDamageReport] no live world to merge a guest's partial-damage report into — the report is neither merged nor answered, and the reporter's pending entry survives to the next cycle.");
			return null;
		}

		var merge = GameBlockDamageTable.Merge(world, reported, "Partial-damage report", log);
		foreach (var damage in merge.Raised)
		{
			// A report that raised this host's own row must show here too: the
			// crack sprite is the local presentation of the same damage.
			damage.UpdateSprite();
		}

		log.LogInformation("[BlockDamageReport] merged {Count} reported cell(s) ({Raised} raised, {Refused} refused).",
			reported.Count, merge.Raised.Count, merge.Refused);
		return merge.Authoritative;
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
		return new NativeRunFields(
			world.lootRarityMultiplier,
			world.trapRarityMultiplier,
			SaveSystem.savedRunTime + world.realTimeElapsed,
			recipes,
			Failure: null);
	}

	/// <inheritdoc />
	public void ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes) => _pendingKeypads = [.. codes];

	/// <inheritdoc />
	public void ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers) => _pendingGeysers = [.. geysers];

	/// <inheritdoc />
	public void ApplyBlockDamages(IReadOnlyList<BlockDamageEntryMsg> damages) => _pendingBlockDamages = [.. damages];

	/// <inheritdoc />
	public void ApplyCutRunFields(float savedRunTime)
	{
		_pendingRunTime = savedRunTime;
		if (!WriteRunFieldsIfPossible())
		{
			log.LogInformation(
				"[SaveFacts] holding the restored run clock base ({RunTime:F1}s) until the live world can take it.",
				savedRunTime);
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
		if (!HasPendingRunFields)
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
	}
}
