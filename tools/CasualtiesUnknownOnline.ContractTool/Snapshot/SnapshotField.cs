using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// One field row. <see cref="IsSerialized"/> applies the Unity rule the tool
/// documents (non-static, non-literal, public or <c>[SerializeField]</c>, minus
/// <c>[NonSerialized]</c>): a serialized field is save-state surface, so it is
/// called out in a report even when its visibility did not move.
/// </summary>
public sealed record SnapshotField(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("visibility")] string Visibility,
	[property: JsonPropertyName("isStatic")] bool IsStatic,
	[property: JsonPropertyName("isLiteral")] bool IsLiteral,
	[property: JsonPropertyName("isReadOnly")] bool IsReadOnly,
	[property: JsonPropertyName("isSerialized")] bool IsSerialized,
	[property: JsonPropertyName("type")] string Type);
