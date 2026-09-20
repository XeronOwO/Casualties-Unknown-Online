using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// The census a snapshot declares about itself, written next to the rows it
/// counts. The reader verifies the declaration against the rows, so a truncated
/// or hand-edited snapshot fails loudly instead of silently reporting fewer
/// members than the build has.
/// </summary>
public sealed record SnapshotCounts(
	[property: JsonPropertyName("types")] int Types,
	[property: JsonPropertyName("methods")] int Methods,
	[property: JsonPropertyName("fields")] int Fields,
	[property: JsonPropertyName("properties")] int Properties,
	[property: JsonPropertyName("enumMembers")] int EnumMembers,
	[property: JsonPropertyName("serializedFields")] int SerializedFields,
	[property: JsonPropertyName("contracts")] int Contracts);
