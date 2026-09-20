using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// One enum member and its constant. The value is rendered as text so an
/// underlying type the tool did not expect cannot silently drop the row; an
/// assembly that carries no constant for the member reports <c>?</c> rather than
/// a made-up zero.
/// </summary>
public sealed record SnapshotEnumMember(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("value")] string Value);
