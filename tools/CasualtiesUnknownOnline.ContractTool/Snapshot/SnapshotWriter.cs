using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CasualtiesUnknownOnline.ContractTool.Json;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// Writes a snapshot in the canonical, byte-reproducible form the ticket's
/// acceptance requires. Reproducibility is a property of THIS code, not of a
/// serializer's defaults: every collection arrives sorted from
/// <see cref="GameAssemblyReader"/>, every property is written in a fixed order,
/// culture never reaches a number or a character, and no timestamp, machine path
/// or environment fact is written at all. The same input file therefore produces
/// byte-identical output on every run.
/// </summary>
public static class SnapshotWriter
{
	private static readonly JsonWriterOptions Options = new()
	{
		Indented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	/// <summary>Writes the snapshot to <paramref name="path"/> (UTF-8, no BOM).</summary>
	public static void Write(SnapshotDocument document, string path)
	{
		using var stream = File.Create(path);
		using var writer = new Utf8JsonWriter(stream, Options);
		WriteDocument(writer, document);
	}

	/// <summary>The same bytes <see cref="Write"/> would put on disk, for round-trip and byte-comparison tests.</summary>
	public static string ToJson(SnapshotDocument document)
	{
		using var stream = new MemoryStream();
		using (var writer = new Utf8JsonWriter(stream, Options))
		{
			WriteDocument(writer, document);
		}

		return Encoding.UTF8.GetString(stream.ToArray());
	}

	internal static void WriteDocument(Utf8JsonWriter writer, SnapshotDocument document)
	{
		writer.WriteStartObject();
		writer.WriteString("schema", document.Schema);
		writer.WriteAssemblyIdentity("assembly", document.Assembly);
		WriteCounts(writer, document);
		writer.WriteStartArray("types");
		foreach (var type in document.Types)
		{
			WriteType(writer, type);
		}

		writer.WriteEndArray();
		writer.WriteStartArray("contracts");
		foreach (var contract in document.Contracts)
		{
			WriteContract(writer, contract);
		}

		writer.WriteEndArray();
		writer.WriteEndObject();
	}

	private static void WriteCounts(Utf8JsonWriter writer, SnapshotDocument document)
	{
		writer.WriteStartObject("counts");
		writer.WriteNumber("types", document.Counts.Types);
		writer.WriteNumber("methods", document.Counts.Methods);
		writer.WriteNumber("fields", document.Counts.Fields);
		writer.WriteNumber("properties", document.Counts.Properties);
		writer.WriteNumber("enumMembers", document.Counts.EnumMembers);
		writer.WriteNumber("serializedFields", document.Counts.SerializedFields);
		writer.WriteNumber("contracts", document.Counts.Contracts);
		writer.WriteEndObject();
	}

	private static void WriteType(Utf8JsonWriter writer, SnapshotType type)
	{
		writer.WriteStartObject();
		writer.WriteString("name", type.Name);
		writer.WriteString("kind", type.Kind);
		writer.WriteString("visibility", type.Visibility);
		if (type.BaseType is null)
		{
			writer.WriteNull("baseType");
		}
		else
		{
			writer.WriteString("baseType", type.BaseType);
		}

		writer.WriteStartArray("methods");
		foreach (var method in type.Methods)
		{
			WriteMethod(writer, method);
		}

		writer.WriteEndArray();
		writer.WriteStartArray("fields");
		foreach (var field in type.Fields)
		{
			WriteField(writer, field);
		}

		writer.WriteEndArray();
		writer.WriteStartArray("properties");
		foreach (var property in type.Properties)
		{
			WriteProperty(writer, property);
		}

		writer.WriteEndArray();
		writer.WriteStartArray("enumMembers");
		foreach (var member in type.EnumMembers)
		{
			writer.WriteStartObject();
			writer.WriteString("name", member.Name);
			writer.WriteString("value", member.Value);
			writer.WriteEndObject();
		}

		writer.WriteEndArray();
		writer.WriteEndObject();
	}

	private static void WriteMethod(Utf8JsonWriter writer, SnapshotMethod method)
	{
		writer.WriteStartObject();
		writer.WriteString("name", method.Name);
		writer.WriteString("visibility", method.Visibility);
		writer.WriteBoolean("isStatic", method.IsStatic);
		writer.WriteBoolean("isAbstract", method.IsAbstract);
		writer.WriteBoolean("isVirtual", method.IsVirtual);
		writer.WriteBoolean("isCompilerGenerated", method.IsCompilerGenerated);
		writer.WriteStringArray("genericParameters", method.GenericParameters);
		writer.WriteString("returns", method.Returns);
		WriteParameters(writer, method.Parameters);
		writer.WriteEndObject();
	}

	private static void WriteField(Utf8JsonWriter writer, SnapshotField field)
	{
		writer.WriteStartObject();
		writer.WriteString("name", field.Name);
		writer.WriteString("visibility", field.Visibility);
		writer.WriteBoolean("isStatic", field.IsStatic);
		writer.WriteBoolean("isLiteral", field.IsLiteral);
		writer.WriteBoolean("isReadOnly", field.IsReadOnly);
		writer.WriteBoolean("isSerialized", field.IsSerialized);
		writer.WriteString("type", field.Type);
		writer.WriteEndObject();
	}

	private static void WriteProperty(Utf8JsonWriter writer, SnapshotProperty property)
	{
		writer.WriteStartObject();
		writer.WriteString("name", property.Name);
		writer.WriteString("visibility", property.Visibility);
		writer.WriteBoolean("isStatic", property.IsStatic);
		writer.WriteBoolean("hasGetter", property.HasGetter);
		writer.WriteBoolean("hasSetter", property.HasSetter);
		writer.WriteString("type", property.Type);
		writer.WriteEndObject();
	}

	private static void WriteParameters(Utf8JsonWriter writer, IReadOnlyList<SnapshotParameter> parameters)
	{
		writer.WriteStartArray("parameters");
		foreach (var parameter in parameters)
		{
			writer.WriteStartObject();
			writer.WriteString("name", parameter.Name);
			writer.WriteString("type", parameter.Type);
			writer.WriteEndObject();
		}

		writer.WriteEndArray();
	}

	private static void WriteContract(Utf8JsonWriter writer, SnapshotContract contract)
	{
		writer.WriteStartObject();
		writer.WriteString("patchClass", contract.PatchClass);
		writer.WriteString("patchClassType", contract.PatchClassType);
		writer.WriteString("targetType", contract.TargetType);
		writer.WriteString("method", contract.Method);
		writer.WriteStringArray("argumentTypes", contract.ArgumentTypes);
		writer.WriteStringArray("patchParameters", contract.PatchParameters);
		writer.WriteEndObject();
	}
}
