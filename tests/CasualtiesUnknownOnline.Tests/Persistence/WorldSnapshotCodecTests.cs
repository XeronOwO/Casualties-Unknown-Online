using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S2's payload contract: the kernel checkpoint becomes the archive's domain
/// files and comes back unchanged. Every file is an entry array (the shape the
/// reader decodes one entry at a time), a layer-end cut writes the two empty
/// world-diff files S3 will fill, and a snapshot without a readable run baseline
/// is refused rather than guessed.
/// </summary>
public class WorldSnapshotCodecTests
{
	private const ulong RunId = 42UL;

	private static readonly ActorId Host = new(1001);

	[Fact]
	public void Encode_WritesOneEntryArrayPerDomainFileAndTheCharacters()
	{
		var authority = StartedAuthority();
		Spawn(authority, 100, "bag", ItemLocation.World(1, 2));

		var files = Encoder().Encode(Payload(authority, ("steam-76561198000000001", Character(100, "bag"))));

		foreach (var expected in new[]
		{
			SaveArchiveFormat.RunFileName,
			SaveArchiveFormat.PlayersFileName,
			SaveArchiveFormat.ItemsFileName,
			SaveArchiveFormat.WorldEntitiesFileName,
			SaveArchiveFormat.EnemiesFileName,
			SaveArchiveFormat.FluidsFileName,
			SaveArchiveFormat.WorldBlocksFileName,
			SaveArchiveFormat.WorldTransientsFileName,
			SaveArchiveFormat.CharacterFilePath("steam-76561198000000001"),
		})
		{
			Assert.True(files.Any(candidate => candidate.Path == expected), $"missing {expected}; the encoder wrote {string.Join(", ", files.Select(file => file.Path))}");
			using var document = JsonDocument.Parse(Assert.Single(files, candidate => candidate.Path == expected).Content);
			Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
		}

		// A layer-end cut has no in-layer deviations: both world-diff files exist
		// empty so S3 can fill them without a schema change (§3.4).
		Assert.Empty(Entries(files, SaveArchiveFormat.WorldBlocksFileName));
		Assert.Empty(Entries(files, SaveArchiveFormat.WorldTransientsFileName));
		Assert.Single(Entries(files, SaveArchiveFormat.RunFileName));
		Assert.Single(Entries(files, SaveArchiveFormat.ItemsFileName));
		Assert.Empty(Entries(files, SaveArchiveFormat.WorldEntitiesFileName));
		Assert.Equal(9, files.Count);
	}

	[Fact]
	public void EncodeThenDecode_RoundTripsEveryDomainAndTheCharacters()
	{
		var authority = StartedAuthority();
		Spawn(authority, 100, "bag", ItemLocation.World(1.5f, -2.25f));
		authority.SyncContainerContents(
			Host.Value,
			100,
			new CharacterItemMsg
			{
				InstanceId = 100,
				ItemId = "bag",
				Condition = 0.75f,
				Contents = [new CharacterItemMsg { InstanceId = 101, ItemId = "water", Condition = 0.5f }],
			},
			Host);
		authority.TryUpdatePlayerStatus(Host.Value, new PlayerState(Host.Value, false, false), out _, out _);
		authority.TryUpdateFluidRegion(Host.Value, new FluidRegionState(1, 2, 7, 1, 50), out _, out _);
		authority.TryRecordOpenedEntity(Host.Value, new EntityPosition(7, 8), out _, out _);
		authority.TryUpsertEnemy(Host.Value, new EnemyState(default, "spider", 4f, false, false), out _, out _);

		var original = authority.CreateCheckpoint();
		var (decoded, salvage) = CodecRoundTrip(original, ("steam-76561198000000001", Character(100, "bag")));

		Assert.True(salvage.IsClean, salvage.Report.Describe());
		Assert.Equal(original.RunEpoch.Value, decoded.RunEpoch.Value);
		Assert.Equal(original.GlobalRevision, decoded.GlobalRevision);
		Assert.Equal(2, decoded.Items.Count);

		var child = decoded.Items.Single(item => item.Identity.InstanceId == 101);
		Assert.Equal(ItemLocationKind.Contained, child.Location.Kind);
		Assert.Equal(100UL, child.Location.ParentItemId);
		Assert.Equal(0.5f, child.Data.Condition);
		Assert.Equal(1.5f, decoded.Items.Single(item => item.Identity.InstanceId == 100).Location.X);

		var run = decoded.Run!;
		Assert.Equal(RunId, run.RunId);
		Assert.Equal([1, 2, 3], run.RandomState);
		Assert.Equal(2, run.BiomeDepth);
		Assert.Equal(42f, Assert.Single(run.RunSettings!).FloatValue);

		Assert.Equal(Host.Value, Assert.Single(decoded.Players!.Players).SteamId);
		Assert.Equal(7, Assert.Single(decoded.Fluids!.Regions).TotalAmount);
		Assert.Single(decoded.WorldEntities!.OpenedEntities);
		Assert.Equal("spider", Assert.Single(decoded.Enemies!.Enemies).PrefabId);
	}

	[Fact]
	public void EncodeThenDecode_RoundTripsTheCharacters()
	{
		var authority = StartedAuthority();

		var (decoded, _) = CodecRoundTrip(authority.CreateCheckpoint(), ("steam-76561198000000001", Character(100, "bag")));

		// The character is a payload file of its own, keyed by the transport-scoped
		// player key: it must come back under the SAME key, with its items intact.
		var character = Assert.Single(RoundTripCharacters(authority.CreateCheckpoint(), ("steam-76561198000000001", Character(100, "bag"))));
		Assert.Equal("steam-76561198000000001", character.PlayerKey);
		Assert.Equal(100UL, Assert.Single(character.Character.Items).InstanceId);
		Assert.Equal(5, character.Character.SlotCount);
		Assert.Equal(RunId, decoded.Run!.RunId);
	}

	[Fact]
	public void Encode_FloatCondition_RoundTripsExactly()
	{
		var authority = StartedAuthority();
		Spawn(authority, 100, "bag", ItemLocation.World(0f, 0f), condition: 0.1f);

		var (decoded, _) = CodecRoundTrip(authority.CreateCheckpoint());

		// System.Text.Json writes the shortest round-trippable form, so an exact
		// comparison is the contract (§3.4), not an epsilon.
		Assert.Equal(0.1f, Assert.Single(decoded.Items).Data.Condition);
	}

	[Fact]
	public void Encode_WithoutARunBaseline_IsRefused()
	{
		var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);

		var error = Assert.Throws<ArgumentException>(() => Encoder().Encode(Payload(authority)));

		Assert.Contains("run baseline", error.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Decode_WithoutARunFile_RefusesTheWholeSnapshot()
	{
		// A snapshot whose run baseline cannot be read must be refused, never
		// silently regenerated into a layer it does not name (§6).
		var (decode, salvage) = Decode([
			SaveTestData.Payload(SaveArchiveFormat.ItemsFileName, "[{\"identity\":{\"instanceId\":1,\"definitionId\":\"stone\"},\"revision\":1,\"location\":{\"kind\":1},\"data\":{}}]"),
		]);

		Assert.Null(decode.Checkpoint);
		Assert.Contains("run baseline", decode.Refusal, StringComparison.Ordinal);

		// The decode pass itself was clean — the snapshot is refused because the RUN
		// BASELINE is missing, not because a row was salvaged away.
		Assert.True(salvage.IsClean, salvage.Report.Describe());
	}

	[Fact]
	public void Decode_RunBaselineWithoutGenerationState_IsRefused()
	{
		var (decode, salvage) = Decode([
			SaveTestData.Payload(SaveArchiveFormat.RunFileName, "[{\"runId\":42,\"randomState\":\"\"}]"),
		]);

		Assert.Null(decode.Checkpoint);
		Assert.Contains("random state", Assert.Single(salvage.SkippedEntries).Detail, StringComparison.Ordinal);
	}

	[Fact]
	public void Decode_UnknownDomainFiles_AreReportedNotGuessed()
	{
		var (decode, salvage) = Decode([
			SaveTestData.Payload(SaveArchiveFormat.RunFileName, RunEntry()),
			SaveTestData.Payload("a-later-stages-domain.json", "[{\"schemaVersion\":1}]"),
		]);

		Assert.NotNull(decode.Checkpoint);
		Assert.Contains("a-later-stages-domain.json", salvage.Report.Describe(), StringComparison.Ordinal);
	}

	[Fact]
	public void Decode_MalformedItemEntry_IsSkippedWhileTheOthersApply()
	{
		// §6: an unmaterializable entry is skipped by itself. The item mapping runs
		// PER ENTRY (the wire→kernel conversion is where a bad row is rejected), so
		// the valid item of the same file still applies instead of the whole domain
		// — or the whole continue — failing.
		var authority = StartedAuthority();
		Spawn(authority, 100, "bag", ItemLocation.World(1, 2));
		var files = Encoder().Encode(Payload(authority));
		var items = Assert.Single(files, file => file.Path == SaveArchiveFormat.ItemsFileName).Content;

		// Rebuild the file from its own decoded first row plus one unrepresentable
		// row (location kind 99 is not a kernel location): the valid row must still
		// apply while the broken one is skipped.
		using var document = JsonDocument.Parse(items);
		var validRow = document.RootElement.EnumerateArray().Single().GetRawText();
		var broken = "[" + validRow + ",\n  {\"identity\":{\"instanceId\":900,\"definitionId\":\"broken\"},\"revision\":1,\"location\":{\"kind\":99},\"data\":{}}\n]";
		var patched = files.Where(file => file.Path != SaveArchiveFormat.ItemsFileName)
			.Append(new SavePayloadFile(SaveArchiveFormat.ItemsFileName, Encoding.UTF8.GetBytes(broken))).ToList();

		var (decode, salvage) = Decode(patched, RunId);

		Assert.NotNull(decode.Checkpoint);
		Assert.True(decode.Checkpoint!.Items.Count == 1, "items were: " + string.Join(", ", decode.Checkpoint.Items.Select(item => item.Identity.InstanceId)) + " | salvage: " + salvage.Report.Describe());
		Assert.Equal(100UL, Assert.Single(decode.Checkpoint.Items).Identity.InstanceId);
		Assert.False(salvage.IsClean, salvage.Report.Describe());
		Assert.Contains(salvage.SkippedEntries, entry => entry.Id.Contains("broken", StringComparison.Ordinal));
	}

	[Fact]
	public void Encode_WithUnpersistedRandomStreams_RefusesTheCut()
	{
		var authority = StartedAuthority();
		var checkpoint = authority.CreateCheckpoint() with { RandomStreams = [new RandomStreamState("world", "seed", [1UL])] };

		// S2 has no file for the RNG streams: writing the cut anyway would drop them
		// and a restore would regenerate a different world, so the cut is refused
		// instead of silently lossy (§6).
		var error = Assert.Throws<NotSupportedException>(() => Encoder().Encode(new WorldSnapshotPayload(checkpoint, [], "Test World", "layer-advance", "layer-boundary")));
		Assert.Contains("random stream", error.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void CharacterPathHelpers_RecogniseOnlyTheCharactersFolder()
	{
		var path = SaveArchiveFormat.CharacterFilePath("steam-76561198000000001");

		Assert.Equal("characters/steam-76561198000000001.json", path);
		Assert.True(SaveArchiveFormat.IsCharacterPath(path));
		Assert.Equal("steam-76561198000000001", SaveArchiveFormat.PlayerKeyOfCharacterPath(path));
		Assert.False(SaveArchiveFormat.IsCharacterPath(SaveArchiveFormat.ItemsFileName));
		Assert.False(SaveArchiveFormat.IsCharacterPath("characters/not-a-json"));
		Assert.Equal(string.Empty, SaveArchiveFormat.PlayerKeyOfCharacterPath(SaveArchiveFormat.ItemsFileName));
	}

	// ---- helpers ----

	internal static WorldSnapshotEncoder Encoder() => new(NullLogger<WorldSnapshotEncoder>.Instance);

	internal static ItemKernelAuthority StartedAuthority(ulong runId = RunId)
	{
		var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		Assert.True(authority.TryStartRun(
			Host.Value,
			new RunState(runId, [1, 2, 3], 0, 2, 10, false, [new RunSetting("speed", RunSettingKind.Float, FloatValue: 42f)]),
			out _,
			out _));
		return authority;
	}

	internal static WorldSnapshotPayload Payload(ItemKernelAuthority authority, params (string Key, CharacterDataMsg Character)[] characters) =>
		new(authority.CreateCheckpoint(), [.. characters.Select(entry => new SavedCharacter(entry.Key, entry.Character))], "Test World", "layer-advance", "layer-boundary");

	internal static void Spawn(ItemKernelAuthority authority, ulong instanceId, string definitionId, ItemLocation location, float condition = 1f) =>
		Assert.True(authority.TrySpawn(
			Host.Value,
			new ItemIdentity(instanceId, definitionId),
			location,
			new CharacterItemMsg { InstanceId = instanceId, ItemId = definitionId, Condition = condition },
			out _,
			out _));

	internal static CharacterDataMsg Character(ulong instanceId, string definitionId) =>
		new() { Items = [new CharacterItemMsg { InstanceId = instanceId, ItemId = definitionId }], SlotCount = 5 };

	/// <summary>Decodes the same payload and returns the characters the decoder produced (the file set is per player key).</summary>
	internal static IReadOnlyList<SavedCharacter> RoundTripCharacters(GameCheckpoint checkpoint, params (string Key, CharacterDataMsg Character)[] characters)
	{
		var payload = new WorldSnapshotPayload(
			checkpoint,
			[.. characters.Select(entry => new SavedCharacter(entry.Key, entry.Character))],
			"Test World",
			"layer-advance",
			"layer-boundary");
		var (decode, _) = Decode(Encoder().Encode(payload), checkpoint.RunEpoch.Value);
		Assert.NotNull(decode.Checkpoint);
		return decode.UsableCharacters;
	}

	internal static (GameCheckpoint Decoded, SalvageResult Salvage) CodecRoundTrip(
		GameCheckpoint checkpoint,
		params (string Key, CharacterDataMsg Character)[] characters)
	{
		var payload = new WorldSnapshotPayload(
			checkpoint,
			[.. characters.Select(entry => new SavedCharacter(entry.Key, entry.Character))],
			"Test World",
			"layer-advance",
			"layer-boundary");
		var files = Encoder().Encode(payload);
		var (decode, salvage) = Decode(files, checkpoint.RunEpoch.Value);
		Assert.NotNull(decode.Checkpoint);
		return (decode.Checkpoint!, salvage);
	}

	/// <summary>Writes the files through the real writer and decodes them back through the real reader.</summary>
	internal static (WorldSnapshotDecode Decode, SalvageResult Salvage) Decode(
		IReadOnlyList<SavePayloadFile> files,
		ulong runEpoch = RunId,
		WorldCutKind kind = WorldCutKind.LayerEnd)
	{
		var test = SaveTestRepository.Create("codec");
		var meta = SaveTestData.Meta(runEpoch: runEpoch.ToString(CultureInfo.InvariantCulture));
		var write = test.Repository.WriteSnapshot(test.WorldId, SaveTestData.Request(test.WorldId, kind, test.Now, meta, [.. files]));
		Assert.True(write.Success, $"{write.Reason}: {write.Detail}");

		var options = new WorldLoadOptions { VerifyChecksums = true };
		var load = test.Repository.LoadSnapshot(test.WorldId, options);
		Assert.True(load.Loaded, load.Summary);

		var decoder = new WorldSnapshotDecoder(load.Content!.Manifest, NullLogger<WorldSnapshotDecoder>.Instance);
		var (_, salvage) = test.Repository.ReadSalvage(load, decoder.DecodeEntry, options);
		return (decoder.Finish(), salvage);
	}

	/// <summary>A run baseline entry the manifest's run epoch can agree with.</summary>
	internal static string RunEntry() =>
		"[{\"runId\":42,\"randomState\":\"AQID\",\"biomeOverride\":0,\"biomeDepth\":2,\"totalTraveled\":10,\"loadedRun\":false,\"layerIndex\":0}]";

	private static JsonElement[] Entries(IReadOnlyList<SavePayloadFile> files, string path)
	{
		using var document = JsonDocument.Parse(Assert.Single(files, file => file.Path == path).Content);
		return [.. document.RootElement.EnumerateArray().Select(element => element.Clone())];
	}
}
