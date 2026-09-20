using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// One parameter of a method or contract target: the name (Harmony matches on
/// it) and the canonical type name (<see cref="TypeNameFormat"/>).
/// </summary>
public sealed record SnapshotParameter(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("type")] string Type);
