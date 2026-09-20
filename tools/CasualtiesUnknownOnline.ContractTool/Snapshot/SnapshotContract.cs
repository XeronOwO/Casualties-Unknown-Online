using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// One patch-target contract row in the form <c>PatchInventory</c> declares it:
/// the patch class, the target type and method, the argument types the
/// <c>[HarmonyPatch]</c> attribute constrains (empty = unconstrained), and the
/// patch parameter names Harmony binds to the target by name.
///
/// The tool reads these from the adapter assembly's METADATA, so a snapshot can
/// be taken without the game running and without the adapter being loaded. The
/// hand-declared dynamic contracts (no attribute — <c>PatchInventory</c> marks
/// them <c>"(dynamic)"</c>) are the one part this lens cannot see; the update-day
/// runbook's contract-test step covers them and the report says so.
/// </summary>
public sealed record SnapshotContract(
	[property: JsonPropertyName("patchClass")] string PatchClass,
	[property: JsonPropertyName("patchClassType")] string PatchClassType,
	[property: JsonPropertyName("targetType")] string TargetType,
	[property: JsonPropertyName("method")] string Method,
	[property: JsonPropertyName("argumentTypes")] IReadOnlyList<string> ArgumentTypes,
	[property: JsonPropertyName("patchParameters")] IReadOnlyList<string> PatchParameters)
{
	/// <summary>
	/// Structural equality across the list members: the compiler-generated record
	/// equality compares <see cref="ArgumentTypes"/> and
	/// <see cref="PatchParameters"/> by reference, which is never true for two
	/// snapshots read from two files. This is the comparison the differ uses to
	/// check that both builds were snapshotted with the same contract set.
	/// </summary>
	public bool HasSameFactsAs(SnapshotContract other) =>
		PatchClass == other.PatchClass
		&& PatchClassType == other.PatchClassType
		&& TargetType == other.TargetType
		&& Method == other.Method
		&& ArgumentTypes.SequenceEqual(other.ArgumentTypes, StringComparer.Ordinal)
		&& PatchParameters.SequenceEqual(other.PatchParameters, StringComparer.Ordinal);
}
