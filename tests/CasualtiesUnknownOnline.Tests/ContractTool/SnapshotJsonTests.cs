using System;
using System.IO;
using System.Text.Json;
using CasualtiesUnknownOnline.ContractTool.Snapshot;
using Xunit;
using static CasualtiesUnknownOnline.Tests.ContractTool.ContractSnapshotBuilder;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// The snapshot artifact's own contract: the canonical writer's bytes depend only
/// on the document (so "the same input produces byte-identical output" is a
/// property of this code, not of a serializer's defaults), every character a
/// game type name can contain survives a round trip, and the reader refuses an
/// artifact it cannot trust instead of reporting a quietly smaller diff.
/// </summary>
public class SnapshotJsonTests
{
	private static readonly string WorkDirectory = Path.Combine(Path.GetTempPath(), "cuo-contract-tool-tests", "json");

	[Fact]
	public void RoundTrip_PreservesEveryRow()
	{
		var document = Sample();
		var path = Write(document, "round-trip.json");

		var readBack = SnapshotReader.Read(path);

		Assert.Equal(SnapshotWriter.ToJson(document), SnapshotWriter.ToJson(readBack));
	}

	[Fact]
	public void Write_IsByteIdenticalAcrossRuns()
	{
		var document = Sample();

		Assert.Equal(SnapshotWriter.ToJson(document), SnapshotWriter.ToJson(Sample()));
	}

	[Fact]
	public void Escaping_RoundTripsQuotesBackslashesControlCharactersAndUnicode()
	{
		var document = Document([Type("Quoted\"Type\\Name\n中", methods: [Method("Say\"Hi")])]);
		var json = SnapshotWriter.ToJson(document);
		var readBack = SnapshotReader.Read(Write(document, "escaping.json"));

		Assert.Contains("\\\"Type", json, StringComparison.Ordinal);
		Assert.Contains("中", json, StringComparison.Ordinal);
		Assert.Equal(json, SnapshotWriter.ToJson(readBack));
	}

	[Fact]
	public void Reader_RefusesAnUnknownSchema()
	{
		var path = Path.Combine(EnsureDirectory(), "unknown-schema.json");
		File.WriteAllText(path, MinimalJson().Replace(SnapshotSchema.Snapshot, "cuo.contract-snapshot/0"));

		var exception = Assert.Throws<InvalidDataException>(() => SnapshotReader.Read(path));

		Assert.Contains("declares schema", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Reader_RefusesACensusThatDisagreesWithItsRows()
	{
		var path = Path.Combine(EnsureDirectory(), "bad-census.json");
		File.WriteAllText(path, MinimalJson().Replace("\"types\": 1", "\"types\": 2"));

		var exception = Assert.Throws<InvalidDataException>(() => SnapshotReader.Read(path));

		Assert.Contains("declares", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Reader_RefusesAMissingFile() => Assert.Throws<FileNotFoundException>(() => SnapshotReader.Read(Path.Combine(EnsureDirectory(), "absent.json")));

	[Fact]
	public void Reader_RefusesRowsThatAreNull()
	{
		var path = Path.Combine(EnsureDirectory(), "null-rows.json");
		File.WriteAllText(path, MinimalJson().Replace("\"methods\": []", "\"methods\": null"));

		var exception = Assert.Throws<InvalidDataException>(() => SnapshotReader.Read(path));

		Assert.Contains("member lists", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Reader_RaisesJsonForAMalformedDocument()
	{
		// A half-written artifact must fail as unreadable input, not as a crash: the CLI
		// turns this JsonException into its documented exit code.
		var path = Path.Combine(EnsureDirectory(), "truncated.json");
		var json = MinimalJson();
		File.WriteAllText(path, json.Substring(0, json.Length / 2));

		Assert.Throws<JsonException>(() => SnapshotReader.Read(path));
	}

	private static SnapshotDocument Sample() => Document(
		[
			Type(
				"FixtureTarget",
				methods: [Method("Carries", "System.Void", "System.Int32"), Method("Plain")],
				fields: [Field("Counter", "System.Int32"), Field("Hidden", "System.Single", "private", isSerialized: false)],
				properties: [new SnapshotProperty("Body", "public", false, true, false, "FixtureTarget")]),
			Type("FixtureMode", "enum", enumMembers: [EnumMember("First", "0"), EnumMember("Second", "2")]),
		],
		[Contract("CarriesPatch", "FixtureTarget", "Carries", ["System.Int32"], ["amount"])]);

	private static string Write(SnapshotDocument document, string name)
	{
		var path = Path.Combine(EnsureDirectory(), name);
		SnapshotWriter.Write(document, path);
		return path;
	}
	private static string EnsureDirectory()
	{
		if (!Directory.Exists(WorkDirectory))
		{
			Directory.CreateDirectory(WorkDirectory);
		}

		return WorkDirectory;
	}

	private static string MinimalJson() =>
		"""
		{
		  "schema": "cuo.contract-snapshot/1",
		  "assembly": { "name": "Fixture", "version": "1.0.0.0", "moduleVersionId": "00000000-0000-0000-0000-000000000000", "sha256": "00" },
		  "counts": { "types": 1, "methods": 0, "fields": 0, "properties": 0, "enumMembers": 0, "serializedFields": 0, "contracts": 0 },
		  "types": [
		    { "name": "FixtureTarget", "kind": "class", "visibility": "public", "baseType": null, "methods": [], "fields": [], "properties": [], "enumMembers": [] }
		  ],
		  "contracts": []
		}
		""";
}
