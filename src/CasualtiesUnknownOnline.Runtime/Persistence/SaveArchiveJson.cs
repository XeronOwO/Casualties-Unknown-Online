using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The one JSON contract for the world archive (docs/architecture/save-archive-format.md
/// §3): UTF-8 without BOM, indented, camelCase names exactly as the document writes
/// them, cut kinds in the format's kebab-case spelling, non-ASCII left readable.
/// Floats round-trip because System.Text.Json emits the shortest round-trippable
/// form — no other numeric handling is configured on purpose.
/// </summary>
internal static class SaveArchiveJson
{
	/// <summary>The serializer settings every archive DTO is read and written with.</summary>
	internal static readonly JsonSerializerOptions Options = CreateOptions();

	private static JsonSerializerOptions CreateOptions()
	{
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = false,
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			// An OPTIONAL archive member that is not recorded is written as ABSENT, never as an
			// explicit null. That matters for a member whose ABSENCE the reader distinguishes
			// from a recorded value — today exactly one: run.json's native-run-fields
			// `layerTimeSpent` (a layer-end cut and an archive written before the value
			// existed both record none, and a written null would make the two
			// indistinguishable in the file itself). The typed-row unions
			// (`SaveRunRow.Run`/`.NativeRunFields`, `SaveWorldBlockRow.*`, `SaveEnemyRow.*`,
			// `SaveWorldEntityRow.*`, `SaveWorldTransientRow.*`) also lose their explicit null
			// payloads here; each of those is already gated by `CarriesItsOwnPayload` or
			// tolerates a missing property, so the omission is a shape change in the file and
			// not a reader contract change.
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		};
		options.Converters.Add(new WorldCutKindJsonConverter());
		options.MakeReadOnly(populateMissingResolver: true);
		return options;
	}

	/// <summary>Serializes with <see cref="Options"/> and indents the result (a writer-only option).</summary>
	internal static byte[] Serialize<T>(T value)
	{
		using var buffer = new MemoryStream();
		using (var document = JsonSerializer.SerializeToDocument(value, Options))
		using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = Options.Encoder }))
		{
			document.WriteTo(writer);
		}

		buffer.WriteByte((byte)'\n');
		return buffer.ToArray();
	}

	/// <summary>Deserializes UTF-8 bytes. Throws <see cref="JsonException"/> when the text is not valid for the target.</summary>
	internal static T? Deserialize<T>(byte[] utf8) => JsonSerializer.Deserialize<T>(utf8, Options);

	/// <summary>
	/// True = the object carries every named property. A DTO with defaults cannot
	/// tell "absent" from "default", so the manifest gate checks presence on the raw
	/// JSON instead of trusting the deserialized object.
	/// </summary>
	internal static bool HasProperties(byte[] utf8, params string[] names)
	{
		using var document = JsonDocument.Parse(utf8);
		if (document.RootElement.ValueKind != JsonValueKind.Object)
		{
			return false;
		}

		foreach (var name in names)
		{
			if (!document.RootElement.TryGetProperty(name, out _))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Writes <paramref name="value"/> to <paramref name="path"/>, creating the directory and flushing to disk.</summary>
	internal static void WriteFile<T>(string path, T value)
	{
		var bytes = Serialize(value);
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
		stream.Write(bytes, 0, bytes.Length);
		stream.Flush(flushToDisk: true);
	}
}
