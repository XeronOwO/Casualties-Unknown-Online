using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// Builds the pieces every save-format test needs: payloads with exact bytes,
/// manifest metadata and a request whose world matches the workspace. The
/// helpers deliberately produce the archive's own JSON dialect (indented, UTF-8)
/// so a test that hand-writes a manifest writes exactly what the writer would.
/// </summary>
internal static class SaveTestData
{
	internal const string ManifestFileName = "manifest.json";
	internal const string RunFileName = "run.json";
	internal const string ItemsFileName = "items.json";
	internal const string ItemId = "kernel.item.001";

	internal static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

	internal static SavePayloadFile Payload(string path, string text) => new(path, Bytes(text));

	internal static SavePayloadFile RunPayload(string marker) =>
		Payload(RunFileName, "{\n  \"schemaVersion\": 1,\n  \"marker\": \"" + marker + "\"\n}\n");

	/// <summary>An items file whose single entry carries <paramref name="id"/> — the entry-salvage subject.</summary>
	internal static SavePayloadFile ItemsPayload(string id) =>
		Payload(ItemsFileName, "[{\"schemaVersion\":1,\"id\":\"" + id + "\",\"kind\":\"stone\"}]\n");

	internal static SavePayloadFile CharacterPayload(string playerKey, string marker) =>
		Payload(SaveArchiveFormat.CharactersFolderName + "/" + playerKey + ".json", "{\"schemaVersion\":1,\"marker\":\"" + marker + "\"}\n");

	internal static SaveManifestMeta Meta(string displayName = "Test World", string runEpoch = "epoch-1") => new()
	{
		DisplayName = displayName,
		GameBuild = "test-build-1",
		CuoBuild = "cuo-test-1",
		ProtocolVersion = 1,
		ContentFingerprint = "fingerprint-1",
		RunEpoch = runEpoch,
		GlobalRevision = 7,
		LayerIndex = 2,
		BiomeDepth = 2,
		PlayerCount = 1,
		CutPhase = "layer-boundary",
		SaveReason = "layer-advance",
	};

	internal static SaveWorldRequest Request(string worldId, WorldCutKind kind, DateTime savedAtUtc, params SavePayloadFile[] payload) => new()
	{
		WorldId = worldId,
		Kind = kind,
		Payload = payload,
		Meta = Meta(),
		SavedAtUtc = savedAtUtc,
	};

	internal static SaveWorldRequest Request(string worldId, WorldCutKind kind, DateTime savedAtUtc, SaveManifestMeta meta, params SavePayloadFile[] payload) => new()
	{
		WorldId = worldId,
		Kind = kind,
		Payload = payload,
		Meta = meta,
		SavedAtUtc = savedAtUtc,
	};

	/// <summary>Serializes a manifest exactly as <see cref="SaveArchiveJson"/> does (indented, UTF-8, trailing newline).</summary>
	internal static byte[] ManifestBytes(SaveManifest manifest)
	{
		var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
		{
			WriteIndented = true,
			Converters = { new JsonStringEnumConverter() },
		});
		return Bytes(json + "\n");
	}
}
