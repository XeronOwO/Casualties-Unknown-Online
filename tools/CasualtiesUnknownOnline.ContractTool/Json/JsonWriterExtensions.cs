using System.Collections.Generic;
using System.Text.Json;
using CasualtiesUnknownOnline.ContractTool.Snapshot;

namespace CasualtiesUnknownOnline.ContractTool.Json;

/// <summary>
/// The two JSON blocks both writers emit — an assembly identity and a string
/// array. They exist once so a snapshot and a diff report can never disagree
/// about how a hash or an argument-type list is spelled.
/// </summary>
public static class JsonWriterExtensions
{
	extension(Utf8JsonWriter writer)
	{
		/// <summary>Writes the build-identity block a report quotes verdicts against, under <paramref name="propertyName"/>.</summary>
		public void WriteAssemblyIdentity(string propertyName, AssemblyIdentity identity)
		{
			writer.WriteStartObject(propertyName);
			writer.WriteString("name", identity.Name);
			writer.WriteString("version", identity.Version);
			writer.WriteString("moduleVersionId", identity.ModuleVersionId);
			writer.WriteString("sha256", identity.Sha256);
			writer.WriteEndObject();
		}

		/// <summary>Writes a string array under <paramref name="propertyName"/> (empty arrays are written as empty).</summary>
		public void WriteStringArray(string propertyName, IReadOnlyList<string> values)
		{
			writer.WriteStartArray(propertyName);
			foreach (var value in values)
			{
				writer.WriteStringValue(value);
			}

			writer.WriteEndArray();
		}
	}
}
