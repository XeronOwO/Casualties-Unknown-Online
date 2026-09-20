using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// One property row. The visibility is the most visible accessor's (Cecil has no
/// property-level visibility), and the accessor methods appear as method rows of
/// their own under their IL names (<c>get_body</c>) — a Harmony target is often
/// one of those, so both shapes are deliberately kept.
/// </summary>
public sealed record SnapshotProperty(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("visibility")] string Visibility,
	[property: JsonPropertyName("isStatic")] bool IsStatic,
	[property: JsonPropertyName("hasGetter")] bool HasGetter,
	[property: JsonPropertyName("hasSetter")] bool HasSetter,
	[property: JsonPropertyName("type")] string Type);
