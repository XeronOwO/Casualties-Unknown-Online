using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// In-memory stand-in for the adapter's native world-fact tables (keypad codes,
/// geyser liquid types and the game's own partial-damage list) and its native run
/// fields (the rarity multipliers, the run clock base and the recipe unlock
/// table) — the optional half of the world-fact seam. It records its calls so a
/// save suite can prove which half of the restore the Runtime owns and which half
/// it hands over, and it carries the same pending lifecycle as the real handover:
/// an apply arms a pending set that exactly one take (or one cancel) ends.
/// </summary>
internal sealed class FakeNativeWorldFacts : INativeWorldFacts
{
	private readonly List<KeypadEntryMsg> _keypads = [];
	private readonly List<GeyserStateEntryMsg> _geysers = [];
	private readonly List<BlockDamageEntryMsg> _damages = [];
	private bool _pending;

	private float _lootRarityMultiplier = RunRarityMultipliers.Neutral;
	private float _trapRarityMultiplier = RunRarityMultipliers.Neutral;
	private float _savedRunTime;
	private float? _layerTimeSpent;
	private readonly List<SaveRecipeUnlockRow> _recipes = [];
	private bool _runFieldsPending;

	internal List<string> Calls { get; } = [];

	internal IReadOnlyList<KeypadEntryMsg> Keypads => _keypads;

	internal IReadOnlyList<GeyserStateEntryMsg> Geysers => _geysers;

	/// <summary>The game's own partial-damage table as this fake holds it.</summary>
	internal IReadOnlyList<BlockDamageEntryMsg> Damages => _damages;

	internal void SeedKeypad(float x, float y, string code) =>
		_keypads.Add(new KeypadEntryMsg { Position = new NetVector2Msg(x, y), Code = code });

	internal void SeedGeyser(float x, float y, byte liquidType) =>
		_geysers.Add(new GeyserStateEntryMsg { Position = new NetVector2Msg(x, y), LiquidType = liquidType });

	internal void SeedBlockDamage(int x, int y, float damage) =>
		_damages.Add(new BlockDamageEntryMsg { X = x, Y = y, Damage = damage });

	/// <summary>Drop the game's own rows WITHOUT arming the restore handover — what the game's own 128-entry eviction or a break does.</summary>
	internal void ClearBlockDamages() => _damages.Clear();

	/// <summary>
	/// The game's own accumulation, as the adapter's relay applies it
	/// (<c>WorldGeneration.DamageBlock</c>: <c>blockDamage.damage += dmg</c>) — the
	/// test-side stand-in for the engine write, so a suite can drive the Runtime's
	/// accounting decisions against the same rows the port reads.
	/// </summary>
	internal void ApplyDamage(int x, int y, float damage)
	{
		var existing = _damages.FirstOrDefault(d => d.X == x && d.Y == y);
		if (existing is null)
		{
			_damages.Add(new BlockDamageEntryMsg { X = x, Y = y, Damage = damage });
			return;
		}

		existing.Damage += damage;
	}

	/// <summary>
	/// Set to make <see cref="Capture"/> report an unreadable table set — the
	/// "no live world" shape, which must refuse a cut rather than come back as an
	/// empty (clean) world.
	/// </summary>
	internal string? CaptureFailure { get; set; }

	public NativeWorldFactCapture Capture()
	{
		Calls.Add("capture-native");
		return CaptureFailure is { } failure
			? NativeWorldFactCapture.Unreadable(failure)
			: new NativeWorldFactCapture([.. _keypads], [.. _geysers], [.. _damages], Failure: null);
	}

	public IReadOnlyList<BlockDamageEntryMsg>? CaptureBlockDamages()
	{
		Calls.Add("capture-block-damages");
		return CaptureFailure is null ? [.. _damages] : null;
	}

	/// <summary>
	/// The live recipe table's unlocked state (the I6 backfill's read — the
	/// adapter's <c>INT == 0</c> rows). Seeding and the adapter's own write are the
	/// same fact, so this is idempotent: a suite that mirrors
	/// <c>RecipeUnlockApply</c> onto this table must not turn one recipe into two
	/// rows.
	/// </summary>
	private readonly List<int> _unlockedRecipes = [];

	internal IReadOnlyList<int> UnlockedRecipes => _unlockedRecipes;

	internal void SeedUnlockedRecipe(int recipeIndex)
	{
		if (!_unlockedRecipes.Contains(recipeIndex))
		{
			_unlockedRecipes.Add(recipeIndex);
		}
	}

	internal void ClearUnlockedRecipes() => _unlockedRecipes.Clear();

	public IReadOnlyList<int>? CaptureUnlockedRecipeIndexes()
	{
		Calls.Add("capture-unlocked-recipes");
		return CaptureFailure is null ? [.. _unlockedRecipes] : null; // CaptureFailure doubles as "no live world" / "the table is not built"
	}

	/// <summary>The native run fields as the "live world" holds them (seeded, or written back by a restore).</summary>
	internal NativeRunFields RunFields => new(
		_lootRarityMultiplier, _trapRarityMultiplier, _savedRunTime, [.. _recipes], CaptureRunFieldsFailure, _layerTimeSpent);

	/// <summary>Seed the layer timer a cut is supposed to read out of the live world (null = the world could not report one).</summary>
	internal void SeedLayerTime(float? layerTimeSpent) => _layerTimeSpent = layerTimeSpent;

	/// <summary>Set to make <see cref="CaptureRunFields"/> report an unreadable read — the "no live world" shape.</summary>
	internal string? CaptureRunFieldsFailure { get; set; }

	/// <summary>Seed the values a cut is supposed to read out of the live world.</summary>
	internal void SeedRunFields(float lootRarity, float trapRarity, float savedRunTime, params SaveRecipeUnlockRow[] recipes)
	{
		_lootRarityMultiplier = lootRarity;
		_trapRarityMultiplier = trapRarity;
		_savedRunTime = savedRunTime;
		_recipes.Clear();
		_recipes.AddRange(recipes);
	}

	public NativeRunFields CaptureRunFields()
	{
		Calls.Add("capture-run-fields");
		return CaptureRunFieldsFailure is { } failure
			? NativeRunFields.Unreadable(failure)
			: RunFields;
	}

	public void ApplyCutRunFields(float savedRunTime, float? layerTimeSpent)
	{
		Calls.Add("apply-cut-run-fields");
		_savedRunTime = savedRunTime;
		if (layerTimeSpent is { } spent)
		{
			_pendingLayerTimeSpent = spent;
			_clockFactsPending = true;
		}

		_runFieldsPending = true;
	}

	public void ApplyRecipeUnlocks(IReadOnlyList<SaveRecipeUnlockRow> recipes)
	{
		Calls.Add("apply-recipe-unlocks");
		// The recipes join the WORLD-ENTRY handover (like the keypads and geysers),
		// not the run-field handover: they need the world's complete recipe table.
		_recipes.Clear();
		_recipes.AddRange(recipes);
		_pending = true;
	}

	public bool ApplyRunGenerationMultipliers(float lootRarityMultiplier, float trapRarityMultiplier)
	{
		Calls.Add("apply-run-multipliers");
		_lootRarityMultiplier = lootRarityMultiplier;
		_trapRarityMultiplier = trapRarityMultiplier;
		return true;
	}

	// ---- The run/layer clocks (the RunFacts message's two values) ----

	/// <summary>The run clock base this "live world" holds.</summary>
	internal float RunClock => _savedRunTime;

	/// <summary>The layer timer this "live world" holds.</summary>
	internal float? LayerTimeSpent => _layerTimeSpent;

	/// <summary>Set to make <see cref="CaptureRunClockFacts"/> report an unreadable read — the "no live world" shape.</summary>
	internal string? CaptureRunClockFailure { get; set; }

	/// <summary>Every value that actually LANDED, in order — the write guard's own record, so a suite can prove a duplicate or a stale value wrote nothing.</summary>
	internal List<string> ClockWrites { get; } = [];

	/// <summary>
	/// The per-world write marker, mirroring the production rules: the clock is written
	/// only when it advances (or when this world has not taken one yet), the layer timer
	/// only when it advances, and the limit only when the world has none.
	/// </summary>
	private bool _sawRunClock;

	private bool _clockFactsPending;

	public RunClockFacts CaptureRunClockFacts()
	{
		Calls.Add("capture-run-clock");
		return CaptureRunClockFailure is { } failure
			? new RunClockFacts(0f, 0f, 0f, failure)
			: new RunClockFacts(_savedRunTime, _layerTimeSpent ?? 0f, _maxTimePerLayer, Failure: null);
	}

	public void ApplyRunFacts(RunClockFacts facts)
	{
		Calls.Add("apply-run-facts");
		if (facts.Failure is not null)
		{
			return;
		}

		_pendingClock = facts.RunClockBase;
		_pendingLayerTimeSpent = facts.LayerTimeSpent;
		_pendingMaxTimePerLayer = facts.MaxTimePerLayer;
		_clockFactsPending = true;
	}

	public void SettleRunClockFacts()
	{
		Calls.Add("settle-run-clock");
		_sawRunClock = false;
		_clockFactsPending = false;
		_pendingClock = null;
		_pendingLayerTimeSpent = null;
		_pendingMaxTimePerLayer = null;
	}

	private float? _pendingClock;
	private float? _pendingLayerTimeSpent;
	private float? _pendingMaxTimePerLayer;
	private float _maxTimePerLayer;

	/// <summary>True = a received run clock or layer timer is waiting for the live world (mirrors the production flag).</summary>
	internal bool HasPendingClockFacts => _clockFactsPending;

	private void FlushClockFacts()
	{
		if (!_clockFactsPending)
		{
			return;
		}

		var clock = _pendingClock ?? 0f;
		var layerTime = _pendingLayerTimeSpent ?? -1f;
		var limit = _pendingMaxTimePerLayer ?? -1f;
		if (clock > 0f && (!_sawRunClock || clock > _savedRunTime))
		{
			_savedRunTime = clock;
			_sawRunClock = true;
			ClockWrites.Add($"clock {clock:F1}");
		}

		if (layerTime > (_layerTimeSpent ?? 0f))
		{
			_layerTimeSpent = layerTime;
			ClockWrites.Add($"layer-time {layerTime:F1}");
		}

		if (limit > 0f && _maxTimePerLayer <= 0f)
		{
			_maxTimePerLayer = limit;
			ClockWrites.Add($"limit {limit:F1}");
		}

		_pendingClock = null;
		_pendingLayerTimeSpent = null;
		_pendingMaxTimePerLayer = null;
		_clockFactsPending = false;
	}

	public bool TryWritePendingRunFields()
	{
		Calls.Add("write-pending-run-fields");
		_runFieldsPending = false;
		FlushClockFacts();
		return true;
	}

	/// <summary>True = a restored run value is waiting for the live world (the production handover's own flag).</summary>
	internal bool HasPendingRunFields => _runFieldsPending;

	public void ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes)
	{
		Calls.Add("apply-keypads");
		// Copy first: the production handover stores a snapshot of the rows, and a
		// caller may legitimately hand over this fake's own current table.
		var snapshot = new List<KeypadEntryMsg>(codes);
		_keypads.Clear();
		_keypads.AddRange(snapshot);
		_pending = true;
	}

	public void ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		Calls.Add("apply-geysers");
		var snapshot = new List<GeyserStateEntryMsg>(geysers);
		_geysers.Clear();
		_geysers.AddRange(snapshot);
		_pending = true;
	}

	public void ApplyBlockDamages(IReadOnlyList<BlockDamageEntryMsg> damages)
	{
		Calls.Add("apply-block-damages");
		var snapshot = new List<BlockDamageEntryMsg>(damages);
		_damages.Clear();
		_damages.AddRange(snapshot);
		_pending = true;
	}

	public bool HasPendingRestore => _pending;

	public NativeWorldFactRestore ReadPendingRestore()
	{
		Calls.Add("read-pending");
		return _pending
			? new NativeWorldFactRestore([.. _keypads], [.. _geysers], [.. _damages], [.. _recipes])
			: NativeWorldFactRestore.Empty;
	}

	public void CommitPendingRestore()
	{
		if (!_pending)
		{
			// Mirrors the production handover: a commit with nothing pending is a
			// no-op that records nothing (only a fully-applied replay commits).
			return;
		}

		Calls.Add("commit-pending");
		_pending = false;
	}

	public void CancelPendingRestore()
	{
		if (!_pending && !_runFieldsPending)
		{
			// Mirrors the production handover: a cancel with nothing pending is a
			// no-op that records nothing (every run start calls it).
			return;
		}

		Calls.Add("cancel-pending");
		_pending = false;
		_runFieldsPending = false;
	}
}
