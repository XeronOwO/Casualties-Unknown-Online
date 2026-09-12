using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One row of <c>run.json</c> (§3.4). The file carries two shapes of fact: the
/// KERNEL's run baseline (the generation random state, the world-defining
/// fields, the typed run settings) and the values only the GAME holds that shape
/// a run but belong to no CUO domain — the accumulated run clock base and the
/// recipe table's unlock state (<c>SaveSystem.savedRunTime</c>,
/// <c>Recipes.recipes[].hasMadeBefore/INT</c>). They are typed rows rather than
/// one packed blob so §6's salvage can skip a bad row by itself, exactly like
/// <c>world-blocks.json</c>.
///
/// The native row exists because native <c>SaveSystem.TryLoadGame</c> used to
/// restore these fields and CUO no longer reads that file (decision 165): the
/// archive has to carry them, and the adapter writes them back at the slot the
/// native load used to run in, before <c>WorldGeneration.Start</c> derives from
/// them.
/// </summary>
public sealed class SaveRunRow
{
	/// <summary>The row kind: <c>run</c> or <c>native-run-fields</c>.</summary>
	public string Kind { get; init; } = string.Empty;

	/// <summary>The kernel baseline — the <c>run</c> row's payload.</summary>
	public WireRunState? Run { get; init; }

	/// <summary>The game's own run-shaping values no CUO domain owns — the <c>native-run-fields</c> row's payload.</summary>
	public SaveNativeRunFields? NativeRunFields { get; init; }

	/// <summary>The kernel run baseline row. Exactly one row of this kind exists in a snapshot.</summary>
	public static SaveRunRow OfRun(WireRunState run) =>
		new() { Kind = RunKind, Run = run };

	/// <summary>The native run-field row (see <see cref="NativeRunFields"/>).</summary>
	public static SaveRunRow OfNativeRunFields(SaveNativeRunFields fields) =>
		new() { Kind = NativeRunFieldsKind, NativeRunFields = fields };

	/// <summary>The row's identity for the damage report.</summary>
	public string Describe() => Kind switch
	{
		RunKind => "run baseline",
		NativeRunFieldsKind => "native run fields (run clock, recipe unlocks)",
		_ => $"<{Kind}>",
	};

	public const string RunKind = "run";

	/// <summary>The game's own run-shaping values: the run clock base and the recipe unlock table.</summary>
	public const string NativeRunFieldsKind = "native-run-fields";
}
