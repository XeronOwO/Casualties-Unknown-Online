using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// One game-assembly build, reduced to the facts a game update can move: the
/// assembly identity, every type with its member signatures, the enum values,
/// the Unity-serialized fields, and the patch-target contract rows the adapter
/// declares.
///
/// The schema id is this tool's own (never the game's), the artifact is written
/// in a canonical byte-reproducible form (<see cref="SnapshotWriter"/>) and is
/// NEVER committed: it carries game-assembly content. Two builds' snapshots are
/// compared by <see cref="Diff.SnapshotDiffer"/> — the classification, not the
/// list, is what the tool exists for.
/// </summary>
public sealed record SnapshotDocument(
	[property: JsonPropertyName("schema")] string Schema,
	[property: JsonPropertyName("assembly")] AssemblyIdentity Assembly,
	[property: JsonPropertyName("counts")] SnapshotCounts Counts,
	[property: JsonPropertyName("types")] IReadOnlyList<SnapshotType> Types,
	[property: JsonPropertyName("contracts")] IReadOnlyList<SnapshotContract> Contracts);
