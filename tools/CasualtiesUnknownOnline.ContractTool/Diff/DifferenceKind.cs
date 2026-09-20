namespace CasualtiesUnknownOnline.ContractTool.Diff;

/// <summary>
/// The classification vocabulary a game-update report answers with. Every
/// difference the tool reports carries exactly one of these, because the list of
/// changes is not the deliverable — the verdict is:
///   - <see cref="RemovedOrRenamed"/>: a type, member or contract target the
///     previous build had is gone; when a same-shape member appeared under
///     another name the row names it as a rename candidate (metadata cannot see
///     a rename, only the pair of a removal and an addition);
///   - <see cref="SignatureChanged"/>: the member is still there but its shape
///     moved — parameter types, return type, names Harmony binds by, visibility
///     or staticness;
///   - <see cref="HarmonyTargetAmbiguous"/>: an unconstrained
///     <c>[HarmonyPatch]</c> target gained an overload, so the hook would bind to
///     an arbitrary one (the same verdict <c>PatchInventory.VerifyMissing</c>
///     reaches at install time);
///   - <see cref="FieldShapeChanged"/>: a field's type, visibility, staticness or
///     serialized status moved (save-state surface);
///   - <see cref="EnumValueChanged"/>: an enum member kept its name and changed
///     its value (wire/save tables keyed on it silently renumber);
///   - <see cref="UnchangedNeedsReview"/>: a contract target is structurally
///     identical, which is exactly the case the structural half CANNOT clear —
///     the members are all still there, and whether they still mean the same
///     thing needs the live game (the future adapter-shell harness);
///   - <see cref="Added"/>: something new that breaks no contract (recorded so the
///     report never hides a change by omission).
/// </summary>
public enum DifferenceKind
{
	/// <summary>A type, member or contract target the previous build had is gone (a rename candidate is named when one exists).</summary>
	RemovedOrRenamed,

	/// <summary>The member exists in both builds with a different shape.</summary>
	SignatureChanged,

	/// <summary>An unconstrained Harmony target gained an overload — the hook would bind arbitrarily.</summary>
	HarmonyTargetAmbiguous,

	/// <summary>A field's type, visibility, staticness or serialized status changed.</summary>
	FieldShapeChanged,

	/// <summary>An enum member kept its name and changed its value.</summary>
	EnumValueChanged,

	/// <summary>A contract target is structurally identical — only the live game can clear it.</summary>
	UnchangedNeedsReview,

	/// <summary>An addition that breaks no contract (counted and listed, not a verdict on a hook).</summary>
	Added,
}
