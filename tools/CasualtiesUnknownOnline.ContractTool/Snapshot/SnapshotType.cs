using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// One type of the snapshotted assembly, keyed by <see cref="Name"/> (the
/// canonical nested-name form, <c>Outer+Inner</c>). Members are split by kind so
/// a field's shape never has to be read out of a method row; enum members and
/// the serialized-field flag are carried here as well, because they are the two
/// facts an update most often moves without touching a signature.
/// </summary>
public sealed record SnapshotType(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("kind")] string Kind,
	[property: JsonPropertyName("visibility")] string Visibility,
	[property: JsonPropertyName("baseType")] string? BaseType,
	[property: JsonPropertyName("methods")] IReadOnlyList<SnapshotMethod> Methods,
	[property: JsonPropertyName("fields")] IReadOnlyList<SnapshotField> Fields,
	[property: JsonPropertyName("properties")] IReadOnlyList<SnapshotProperty> Properties,
	[property: JsonPropertyName("enumMembers")] IReadOnlyList<SnapshotEnumMember> EnumMembers);
