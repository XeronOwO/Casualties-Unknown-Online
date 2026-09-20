namespace CasualtiesUnknownOnline.ContractTool.Diff;

/// <summary>
/// The machine keys, the printable labels and the report order for the
/// classification vocabulary. One place, so the JSON a test asserts on and the
/// markdown a human reads can never drift apart.
/// </summary>
public static class DifferenceVocabulary
{
	/// <summary>The stable JSON key of a kind.</summary>
	public static string Key(DifferenceKind kind) => kind switch
	{
		DifferenceKind.RemovedOrRenamed => "removed-or-renamed",
		DifferenceKind.SignatureChanged => "signature-changed",
		DifferenceKind.HarmonyTargetAmbiguous => "harmony-target-ambiguous",
		DifferenceKind.FieldShapeChanged => "field-shape-changed",
		DifferenceKind.EnumValueChanged => "enum-value-changed",
		DifferenceKind.UnchangedNeedsReview => "unchanged-needs-review",
		DifferenceKind.Added => "added",
		_ => "unknown",
	};

	/// <summary>The printable label of a kind.</summary>
	public static string Label(DifferenceKind kind) => kind switch
	{
		DifferenceKind.RemovedOrRenamed => "Removed or renamed",
		DifferenceKind.SignatureChanged => "Signature changed",
		DifferenceKind.HarmonyTargetAmbiguous => "Harmony target ambiguous",
		DifferenceKind.FieldShapeChanged => "Field shape changed",
		DifferenceKind.EnumValueChanged => "Enum value changed",
		DifferenceKind.UnchangedNeedsReview => "Unchanged, still needs a semantic look",
		DifferenceKind.Added => "Added",
		_ => "Unknown",
	};

	/// <summary>The stable JSON key of a scope.</summary>
	public static string Key(DifferenceScope scope) => scope switch
	{
		DifferenceScope.Contract => "contract",
		DifferenceScope.ContractAdjacent => "contract-adjacent",
		DifferenceScope.OutsideContract => "outside-contract",
		_ => "unknown",
	};

	/// <summary>The printable label of a scope (used as a report section heading).</summary>
	public static string Label(DifferenceScope scope) => scope switch
	{
		DifferenceScope.Contract => "Contract verdicts",
		DifferenceScope.ContractAdjacent => "Contract-adjacent changes",
		DifferenceScope.OutsideContract => "Outside the contract lens",
		_ => "Unknown",
	};

	/// <summary>The printed spelling of a boolean in a difference detail (a report is prose, not C#).</summary>
	public static string Flag(bool value) => value ? "true" : "false";

	/// <summary>Report/sort order: scope first (hook work before noise), then severity of the kind.</summary>
	public static int Order(DifferenceScope scope) => scope switch
	{
		DifferenceScope.Contract => 0,
		DifferenceScope.ContractAdjacent => 1,
		DifferenceScope.OutsideContract => 2,
		_ => 3,
	};

	/// <summary>Report/sort order within a scope.</summary>
	public static int Order(DifferenceKind kind) => kind switch
	{
		DifferenceKind.RemovedOrRenamed => 0,
		DifferenceKind.SignatureChanged => 1,
		DifferenceKind.HarmonyTargetAmbiguous => 2,
		DifferenceKind.FieldShapeChanged => 3,
		DifferenceKind.EnumValueChanged => 4,
		DifferenceKind.UnchangedNeedsReview => 5,
		DifferenceKind.Added => 6,
		_ => 7,
	};
}
