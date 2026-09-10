using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Writes a cut kind as the format spells it (<c>layer-end</c>, <c>mid-run</c>,
/// <c>auto</c>) and reads that spelling back through the same table. Without this
/// the file would carry the enum member name, and the file and
/// docs/architecture/save-archive-format.md §3.2 would drift apart.
/// </summary>
internal sealed class WorldCutKindJsonConverter : JsonConverter<WorldCutKind>
{
	public override WorldCutKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var text = reader.GetString();
		return SaveArchiveFormat.TryParseCutKind(text, out var kind)
			? kind
			: throw new JsonException($"'{text}' is not a cut kind of this format.");
	}

	public override void Write(Utf8JsonWriter writer, WorldCutKind value, JsonSerializerOptions options) =>
		writer.WriteStringValue(SaveArchiveFormat.CutKindName(value));
}
