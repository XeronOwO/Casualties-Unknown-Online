using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using CasualtiesUnknownOnline.ContractTool.Json;
using CasualtiesUnknownOnline.ContractTool.Snapshot;

namespace CasualtiesUnknownOnline.ContractTool.Diff;

/// <summary>
/// Writes the classified diff as canonical JSON — the machine-readable half a
/// test or a follow-up tool keys on, while the markdown report stays the human
/// artifact. Same discipline as the snapshot writer: fixed property order, no
/// timestamps, no machine facts, so the bytes depend only on the comparison.
/// </summary>
public static class DiffWriter
{
	private static readonly JsonWriterOptions Options = new()
	{
		Indented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	/// <summary>Writes the diff document to <paramref name="path"/>.</summary>
	public static void Write(DiffResult result, string path)
	{
		using var stream = File.Create(path);
		using var writer = new Utf8JsonWriter(stream, Options);
		WriteDocument(writer, result);
	}

	internal static void WriteDocument(Utf8JsonWriter writer, DiffResult result)
	{
		writer.WriteStartObject();
		writer.WriteString("schema", SnapshotSchema.Diff);
		writer.WriteAssemblyIdentity("previous", result.Previous.Assembly);
		writer.WriteAssemblyIdentity("current", result.Current.Assembly);
		WriteLens(writer, result);
		WriteSummary(writer, result);
		writer.WriteStartArray("differences");
		foreach (var difference in result.Differences)
		{
			writer.WriteStartObject();
			writer.WriteString("kind", DifferenceVocabulary.Key(difference.Kind));
			writer.WriteString("scope", DifferenceVocabulary.Key(difference.Scope));
			writer.WriteString("subject", difference.Subject);
			writer.WriteString("previous", difference.Previous);
			writer.WriteString("current", difference.Current);
			writer.WriteString("detail", difference.Detail);
			writer.WriteEndObject();
		}

		writer.WriteEndArray();
		writer.WriteEndObject();
	}

	private static void WriteLens(Utf8JsonWriter writer, DiffResult result)
	{
		writer.WriteStartObject("lens");
		WriteLensSide(writer, "previous", result.PreviousLens);
		WriteLensSide(writer, "current", result.CurrentLens);
		writer.WriteEndObject();
	}

	private static void WriteLensSide(Utf8JsonWriter writer, string propertyName, ContractLens lens)
	{
		writer.WriteStartObject(propertyName);
		writer.WriteNumber("resolved", lens.Resolved);
		writer.WriteNumber("ambiguous", lens.Ambiguous);
		writer.WriteNumber("unresolved", lens.Unresolved);
		writer.WriteEndObject();
	}

	private static void WriteSummary(Utf8JsonWriter writer, DiffResult result)
	{
		writer.WriteStartObject("summary");
		writer.WriteBoolean("brokenContracts", result.HasBrokenContracts);
		writer.WriteNumber("differences", result.Differences.Count);
		foreach (var kind in new[]
		{
			DifferenceKind.RemovedOrRenamed,
			DifferenceKind.SignatureChanged,
			DifferenceKind.HarmonyTargetAmbiguous,
			DifferenceKind.FieldShapeChanged,
			DifferenceKind.EnumValueChanged,
			DifferenceKind.UnchangedNeedsReview,
			DifferenceKind.Added,
		})
		{
			writer.WriteNumber(DifferenceVocabulary.Key(kind), result.Count(kind));
		}

		writer.WriteEndObject();
	}
}
