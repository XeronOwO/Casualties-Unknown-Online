using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S3.1's payload skeleton: the world diff and the transient world facts travel
/// as typed rows in <c>world-blocks.json</c> and <c>world-transients.json</c>,
/// a bad row is salvaged by itself while the rest of its file still applies, and
/// a layer-end cut keeps writing the two empty arrays S2 shipped.
///
/// The row types are tested here; the S3.2 world-diff capture and the S3.3
/// consistent cut are what fill them in production.
/// </summary>
public class WorldSnapshotWorldFactsTests
{
	private const ulong RunId = 42UL;

	// ---- encoded shape ----

	[Fact]
	public void Encode_LayerEndCut_WritesBothWorldFilesEmpty()
	{
		var payload = Payload(Kind: WorldCutKind.LayerEnd);

		var blocks = Entries(Encoder().Encode(payload), SaveArchiveFormat.WorldBlocksFileName);
		var transients = Entries(Encoder().Encode(payload), SaveArchiveFormat.WorldTransientsFileName);

		// A layer-end cut records no in-layer fact: the layer it names is
		// regenerated from the run baseline (§3.4/§4).
		Assert.Empty(blocks);
		Assert.Empty(transients);
	}

	[Fact]
	public void Encode_LayerEndCut_DropsFactsItWasHandedInsteadOfWritingThemAway()
	{
		// The encoder owns the rule, not the caller: a cut that gathered the live
		// tables for a layer-end cut must not silently write them into a snapshot
		// whose layer will be regenerated. It writes the empty arrays it must.
		var payload = Payload(
			Kind: WorldCutKind.LayerEnd,
			blocks: [SaveWorldBlockRow.OfBlockState(3, 4, 0)],
			transients: [SaveWorldTransientRow.OfKeypad(Keypad(1, 2, "1234"))]);

		var files = Encoder().Encode(payload);

		Assert.Empty(Entries(files, SaveArchiveFormat.WorldBlocksFileName));
		Assert.Empty(Entries(files, SaveArchiveFormat.WorldTransientsFileName));
	}

	[Fact]
	public void Encode_MidRunCut_WritesOneTypedRowPerFact()
	{
		var payload = Payload(
			Kind: WorldCutKind.MidRun,
			blocks:
			[
				SaveWorldBlockRow.OfBlockState(3, 4, 0),
				SaveWorldBlockRow.OfNativeBlockDamage(5, 6, 0.25f),
			],
			transients:
			[
				SaveWorldTransientRow.OfRadiationLine(Radiation(active: true, timeGone: 12.5f)),
			]);

		var files = Encoder().Encode(payload);

		using var blocks = JsonDocument.Parse(Content(files, SaveArchiveFormat.WorldBlocksFileName));
		var blockRows = blocks.RootElement.EnumerateArray().ToList();
		Assert.Equal(2, blockRows.Count);
		Assert.Equal("block-state", blockRows[0].GetProperty("kind").GetString());
		Assert.Equal(3, blockRows[0].GetProperty("blockState").GetProperty("x").GetInt32());
		Assert.Equal(0, blockRows[0].GetProperty("blockState").GetProperty("block").GetInt32());
		Assert.Equal("native-block-damage", blockRows[1].GetProperty("kind").GetString());

		using var transients = JsonDocument.Parse(Content(files, SaveArchiveFormat.WorldTransientsFileName));
		var transientRows = transients.RootElement.EnumerateArray().ToList();
		Assert.Equal("radiation-line", Assert.Single(transientRows).GetProperty("kind").GetString());
		Assert.True(transientRows[0].GetProperty("radiationLine").GetProperty("active").GetBoolean());
	}

	// ---- round trip ----

	[Fact]
	public void Codec_RoundTripsBothBlockRowShapes()
	{
		var blocks = new List<SaveWorldBlockRow>
		{
			SaveWorldBlockRow.OfBlockState(-3, 4, 65535),
			SaveWorldBlockRow.OfNativeBlockDamage(7, -8, 0.1f),
		};

		var (decode, salvage) = RoundTrip(blocks: blocks);

		Assert.True(salvage.IsClean, salvage.Report.Describe());
		var rows = decode.UsableWorldBlocks;
		Assert.Equal(2, rows.Count);

		var state = Assert.Single(rows, row => row.Kind == SaveWorldBlockRow.BlockStateKind);
		Assert.Equal(-3, state.BlockState!.X);
		Assert.Equal(4, state.BlockState.Y);
		Assert.Equal(65535, state.BlockState.Block);

		var damage = Assert.Single(rows, row => row.Kind == SaveWorldBlockRow.NativeBlockDamageKind);
		Assert.Equal(0.1f, damage.NativeBlockDamage!.Damage);
	}

	[Fact]
	public void Codec_RoundTripsEveryTransientRowShape()
	{
		var transients = new List<SaveWorldTransientRow>
		{
			SaveWorldTransientRow.OfKeypad(Keypad(1.5f, -2.25f, "4321")),
			SaveWorldTransientRow.OfGeyser(new GeyserStateEntryMsg { Position = new NetVector2Msg(9f, 10f), LiquidType = 2 }),
			SaveWorldTransientRow.OfRadiationLine(Radiation(active: false, timeGone: 0.75f)),
		};

		var (decode, salvage) = RoundTrip(transients: transients);

		Assert.True(salvage.IsClean, salvage.Report.Describe());
		var rows = decode.UsableWorldTransients;
		Assert.Equal(3, rows.Count);

		var keypad = Assert.Single(rows, row => row.Kind == SaveWorldTransientRow.KeypadKind).Keypad!;
		Assert.Equal("4321", keypad.Code);
		Assert.Equal(1.5f, keypad.Position.X);
		Assert.Equal(-2.25f, keypad.Position.Y);

		var geyser = Assert.Single(rows, row => row.Kind == SaveWorldTransientRow.GeyserKind).Geyser!;
		Assert.Equal(2, geyser.LiquidType);
		Assert.Equal(10f, geyser.Position.Y);

		var radiation = Assert.Single(rows, row => row.Kind == SaveWorldTransientRow.RadiationLineKind).RadiationLine!;
		Assert.False(radiation.Active);
		Assert.Equal(0.75f, radiation.TimeGone);
	}

	[Fact]
	public void Codec_EmptyFactFiles_DecodeToNothing()
	{
		var (decode, salvage) = RoundTrip();

		Assert.True(salvage.IsClean, salvage.Report.Describe());
		Assert.Empty(decode.UsableWorldBlocks);
		Assert.Empty(decode.UsableWorldTransients);
	}

	// ---- per-entry salvage ----

	[Fact]
	public void Decode_UnknownBlockKind_IsSkippedWhileTheKnownRowsApply()
	{
		var (decode, salvage) = RoundTrip(
			rawBlocks:
			[
				StateRow(1, 2, 9),
				"{\"kind\":\"block-explosion\",\"x\":3,\"y\":4}",
			]);

		Assert.NotNull(decode.Checkpoint);
		var row = Assert.Single(decode.UsableWorldBlocks);
		Assert.Equal(SaveWorldBlockRow.BlockStateKind, row.Kind);
		Assert.False(salvage.IsClean);
		Assert.Contains(salvage.SkippedEntries, entry => entry.Detail.Contains("block-explosion", StringComparison.Ordinal));
	}

	[Fact]
	public void Decode_UnknownTransientKind_IsSkippedWhileTheKnownRowsApply()
	{
		var (decode, salvage) = RoundTrip(
			rawTransients:
			[
				GeyserRow(4f, 5f, 1),
				"{\"kind\":\"earthquake\",\"duration\":4}",
			]);

		Assert.NotNull(decode.Checkpoint);
		Assert.Equal(SaveWorldTransientRow.GeyserKind, Assert.Single(decode.UsableWorldTransients).Kind);
		Assert.Contains(salvage.SkippedEntries, entry => entry.Detail.Contains("earthquake", StringComparison.Ordinal));
	}

	[Fact]
	public void Decode_RowThatNamesAKindWithoutItsPayload_IsSkippedAndTheRestApply()
	{
		// The kind is one this build knows, so it is NOT a newer writer's row: a
		// kind with no payload is a malformed row and is reported, never guessed.
		var (decode, salvage) = RoundTrip(
			rawBlocks:
			[
				"{\"kind\":\"block-state\"}",
				DamageRow(5, 6, 0.5f),
			],
			rawTransients:
			[
				"{\"kind\":\"keypad\"}",
				RadiationRow(active: true, timeGone: 1f),
			]);

		Assert.Equal(SaveWorldBlockRow.NativeBlockDamageKind, Assert.Single(decode.UsableWorldBlocks).Kind);
		Assert.Equal(SaveWorldTransientRow.RadiationLineKind, Assert.Single(decode.UsableWorldTransients).Kind);
		Assert.Contains(salvage.SkippedEntries, entry => entry.Id.Contains("block-state", StringComparison.Ordinal));
		Assert.Contains(salvage.SkippedEntries, entry => entry.Id.Contains("keypad", StringComparison.Ordinal));
	}

	[Fact]
	public void Decode_UnreadableRow_IsSkippedByItself()
	{
		var (decode, salvage) = RoundTrip(
			rawTransients:
			[
				KeypadRow(1f, 2f, "1234"),
				"{\"kind\":\"geyser\",\"geyser\":{\"liquidType\":\"not-a-number\"}}",
			]);

		Assert.Equal(SaveWorldTransientRow.KeypadKind, Assert.Single(decode.UsableWorldTransients).Kind);
		Assert.Contains(salvage.SkippedEntries, entry => entry.Reason == DamageReport.EntryReason.ContentMissing);
	}

	[Fact]
	public void Decode_JsonNullEntry_IsReportedNotSilentlyDropped()
	{
		// `null` deserializes to null WITHOUT throwing, so without an explicit check
		// the row would vanish from the account entirely (§6: every skip surfaced).
		var (decode, salvage) = RoundTrip(
			rawBlocks:
			[
				"null",
				StateRow(1, 2, 9),
			]);

		Assert.Equal(SaveWorldBlockRow.BlockStateKind, Assert.Single(decode.UsableWorldBlocks).Kind);
		Assert.False(salvage.IsClean);
		Assert.Contains(salvage.SkippedEntries, entry => entry.Detail.Contains("JSON null", StringComparison.Ordinal));
	}

	[Fact]
	public void Decode_PayloadMissingItsFields_IsSkippedInsteadOfApplyingTypeDefaults()
	{
		// The dangerous shape: a payload object with no cell/position would
		// deserialize to (0,0) / 0 and be written onto the world as if recorded.
		var (decode, salvage) = RoundTrip(
			rawBlocks:
			[
				"{\"kind\":\"block-state\",\"blockState\":{}}",
				"{\"kind\":\"block-damage\",\"blockDamage\":{\"x\":5}}",
				StateRow(1, 2, 9),
			],
			rawTransients:
			[
				"{\"kind\":\"radiation-line\",\"radiationLine\":{}}",
				"{\"kind\":\"keypad\",\"keypad\":{\"code\":\"1234\"}}",
				GeyserRow(4f, 5f, 1),
			]);

		Assert.Equal(SaveWorldBlockRow.BlockStateKind, Assert.Single(decode.UsableWorldBlocks).Kind);
		Assert.Equal(SaveWorldTransientRow.GeyserKind, Assert.Single(decode.UsableWorldTransients).Kind);
		Assert.False(salvage.IsClean);
		Assert.Equal(4, salvage.SkippedEntries.Count);
	}

	[Fact]
	public void Decode_TransientRowWithAnEmptyOrPartialPosition_IsSkipped()
	{
		// A transient fact is keyed by its entity's world position: an empty position
		// object would deserialize to a real-looking (0,0) and be handed to the
		// applier as if the code belonged to that entity.
		var (decode, salvage) = RoundTrip(
			rawTransients:
			[
				"{\"kind\":\"keypad\",\"keypad\":{\"position\":{},\"code\":\"1234\"}}",
				"{\"kind\":\"keypad\",\"keypad\":{\"position\":{\"y\":9},\"code\":\"5678\"}}",
				GeyserRow(4f, 5f, 1),
			]);

		Assert.Equal(SaveWorldTransientRow.GeyserKind, Assert.Single(decode.UsableWorldTransients).Kind);
		Assert.Equal(2, salvage.SkippedEntries.Count);
	}

	[Fact]
	public void Decode_LayerEndManifest_WithInLayerFacts_IsRefused()
	{
		// The manifest names what the snapshot IS: a layer-end cut records no
		// in-layer fact, so a layer-end snapshot carrying one would graft a layer's
		// mutations onto a regenerated layer. Refused, never applied or dropped.
		// The file is written by hand because this build's ENCODER never produces
		// that combination (a layer-end cut writes the empty arrays).
		var files = Replace(
			Encoder().Encode(Payload(Kind: WorldCutKind.LayerEnd)),
			SaveArchiveFormat.WorldBlocksFileName,
			ArrayOf([StateRow(3, 4, 0)]));

		var (decode, _) = WorldSnapshotCodecTests.Decode(files, RunId, WorldCutKind.LayerEnd);

		Assert.Null(decode.Checkpoint);
		Assert.Contains("layer-end cut", decode.Refusal, StringComparison.Ordinal);
	}

	[Fact]
	public void LayerAdvanceCut_KeepsTheTwoWorldFilesEmptyAndReadsNoFactTable()
	{
		using var fixture = WorldSaveFixture.Create("facts-layer-advance");
		fixture.WorldFacts.SeedBlockState(3, 4, 0);
		fixture.WorldFacts.RadiationLine = Radiation(active: true, timeGone: 3f);
		Assert.True(fixture.Service.TryBeginRun());
		Assert.True(fixture.Kernel.TryStartRun(1001UL, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		fixture.Characters.SaveHostCharacterData(WorldSaveCaptureTests.Character(100, "bag"));

		// The kernel's own commit is the cut trigger — this is the S2 path, and it
		// must stay exactly as S2 left it: no in-layer fact written, and no read of
		// the tables either (a read whose result is then thrown away is the silent
		// drop §6 forbids).
		Assert.True(fixture.Kernel.TryAdvanceLayer(1001UL, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));

		Assert.Empty(fixture.WorldFacts.Calls);

		// The written snapshot's own form: the two files exist (so S3 fills them
		// without a schema change) and hold the empty array S2 shipped.
		var live = fixture.Repository.Workspace.LiveDirectory(fixture.WorldId);
		Assert.Equal("[]", ReadLiveEntry(live, SaveArchiveFormat.WorldBlocksFileName));
		Assert.Equal("[]", ReadLiveEntry(live, SaveArchiveFormat.WorldTransientsFileName));
	}

	/// <summary>The live snapshot's file, whitespace and trailing newline stripped — the payload form itself.</summary>
	private static string ReadLiveEntry(string liveDirectory, string fileName) =>
		File.ReadAllText(Path.Combine(liveDirectory, fileName)).Trim();

	// ---- the production seam, over the real world composition ----

	[Fact]
	public void WorldFactPort_CapturesTheLiveTablesAndAppliesARestoreAbsolutely()
	{
		using var world = ItemSimWorld.Create();
		var control = world.Host.Services.GetRequiredService<IWorldControl>();
		var facts = world.Host.Services.GetRequiredService<IWorldFactSource>();

		// The port resolves to the world control's own lifecycle — the save layer
		// has to read exactly the tables the live world writes.
		Assert.Same(control, facts);

		control.ReportBlockState(3, 4, 0);
		control.ReportBlockState(5, 6, 12);
		control.BroadcastRadiationLineState(Radiation(active: true, timeGone: 4f));

		var captured = facts.CaptureBlockStates();
		Assert.Equal(2, captured.Count);
		Assert.Contains(captured, entry => entry is { X: 3, Y: 4, Block: 0 });
		Assert.Equal(4f, facts.CaptureRadiationLine()!.TimeGone);

		// A restored cut is applied ABSOLUTELY: cells the cut does not name are gone
		// (the lifecycle resets the tables first), and the radiation line comes back
		// as the cut recorded it.
		facts.ApplyFacts(
			[new BlockStateEntryMsg { X = 3, Y = 4, Block = 0 }],
			Radiation(active: false, timeGone: 9f));

		var restored = facts.CaptureBlockStates();
		Assert.True(Assert.Single(restored) is { X: 3, Y: 4, Block: 0 }, "the cell the cut does not name survived the restore");
		Assert.Equal(9f, facts.CaptureRadiationLine()!.TimeGone);

		// A new LAYER still resets only the per-layer tables: the radiation line is
		// run state the layer boundary never touched, and applying a cut with no
		// radiation row does clear it (the restore replaces the whole fact set).
		control.ResetDamagedBlocks();
		Assert.Empty(facts.CaptureBlockStates());
		Assert.NotNull(facts.CaptureRadiationLine());

		facts.ApplyFacts([], radiationLine: null);
		Assert.Null(facts.CaptureRadiationLine());
	}

	[Fact]
	public void WorldFactPort_Restore_LeavesTheKernelBackedWorldEntitiesAlone()
	{
		// The restore runs AFTER `_kernel.Restore`, so the save layer's reset must
		// not touch the kernel-backed world-entity tables: those registries write
		// through to the kernel (TryResetWorldEntities), and clearing them here would
		// erase the opened/consumed/building-health facts the restore just applied.
		using var world = ItemSimWorld.Create();
		var control = world.Host.Services.GetRequiredService<IWorldControl>();
		var facts = world.Host.Services.GetRequiredService<IWorldFactSource>();
		var kernel = world.Host.Services.GetRequiredService<ItemKernelAuthority>();

		control.FireBuildingEntityOpenedReceived(new NetVector2(5f, 6f));
		Assert.Single(kernel.CreateCheckpoint().WorldEntities!.OpenedEntities);

		// A live table with content, so the restore's reset really has work to do.
		control.ReportBlockState(3, 4, 0);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 9, Y = 9, Block = 1 }], radiationLine: null);

		Assert.Equal((9, 9), (Assert.Single(facts.CaptureBlockStates()).X, facts.CaptureBlockStates()[0].Y));
		Assert.True(
			kernel.CreateCheckpoint().WorldEntities is { OpenedEntities.Count: 1 },
			"the restore's reset erased the kernel-backed world-entity facts");
	}

	[Fact]
	public void WorldFactPort_Restore_MarksTheLiveReplayPendingUntilTheAdapterConsumesIt()
	{
		using var world = ItemSimWorld.Create();
		var control = world.Host.Services.GetRequiredService<IWorldControl>();
		var facts = world.Host.Services.GetRequiredService<IWorldFactSource>();

		Assert.False(facts.HasPendingLiveReplay, "a fresh world has nothing to replay");

		facts.ApplyFacts([new BlockStateEntryMsg { X = 3, Y = 4, Block = 0 }], radiationLine: null);
		Assert.True(facts.HasPendingLiveReplay, "a restored cut owns the next generation's cache state");

		// The adapter's world-entry replay clears the marker when the live world has
		// the facts — and clearing it never touches the facts themselves (they are
		// the table the peers are snapshotted from).
		facts.ClearPendingLiveReplay();
		Assert.False(facts.HasPendingLiveReplay);
		Assert.Single(facts.CaptureBlockStates());

		// A LAYER reset ends a pending replay as well: the facts it was waiting for
		// went with the reset, so a later generation must not be handed them.
		facts.ApplyFacts([new BlockStateEntryMsg { X = 7, Y = 8, Block = 0 }], radiationLine: null);
		Assert.True(facts.HasPendingLiveReplay);
		control.ResetDamagedBlocks();
		Assert.False(facts.HasPendingLiveReplay);
		Assert.Empty(facts.CaptureBlockStates());
	}

	// ---- row descriptions (the repair report's identity for a skipped row) ----
	[Fact]
	public void Describe_NamesTheCellOrTheEntityForEveryKind()
	{
		Assert.Equal("block-state at (3,4)", SaveWorldBlockRow.OfBlockState(3, 4, 7).Describe());
		Assert.Equal("native-block-damage at (5,6)", SaveWorldBlockRow.OfNativeBlockDamage(5, 6, 0.5f).Describe());
		Assert.Equal("<block-mystery>", new SaveWorldBlockRow { Kind = "block-mystery" }.Describe());

		Assert.Equal("keypad at (1,2)", SaveWorldTransientRow.OfKeypad(Keypad(1, 2, "0")).Describe());
		Assert.Equal("geyser at (3,4)", SaveWorldTransientRow.OfGeyser(new GeyserStateEntryMsg { Position = new NetVector2Msg(3f, 4f), LiquidType = 1 }).Describe());
		Assert.Equal("radiation-line active=True", SaveWorldTransientRow.OfRadiationLine(Radiation(active: true, timeGone: 1f)).Describe());
		Assert.Equal("<mystery>", new SaveWorldTransientRow { Kind = "mystery" }.Describe());
	}

	// ---- the save service's fact seam ----

	[Fact]
	public void CaptureWorldFacts_LayerEndCut_CarriesNothingAndTouchesNoTable()
	{
		using var fixture = WorldSaveFixture.Create("facts-layer-end");
		fixture.WorldFacts.SeedBlockState(1, 2, 0);
		fixture.WorldFacts.RadiationLine = new RadiationLineStateMsg { Active = true, TimeGone = 2f };

		var facts = fixture.Writer.CaptureWorldFacts(WorldCutReason.LayerAdvance, WorldCutKind.LayerEnd);

		Assert.Empty(facts.Blocks);
		Assert.Empty(facts.Transients);
		Assert.Empty(fixture.WorldFacts.Calls);
	}

	[Fact]
	public void CaptureMidRunFacts_MapsEveryRuntimeFactIntoItsRowShape()
	{
		using var fixture = WorldSaveFixture.Create("facts-mid-run");
		fixture.WorldFacts.SeedBlockState(1, 2, 0);
		fixture.WorldFacts.RadiationLine = new RadiationLineStateMsg { Active = true, TimeGone = 2f };

		var facts = fixture.Writer.CaptureMidRunFacts(WorldCutReason.MenuReturn, WorldCutKind.MidRun);

		// The Runtime half carries the block diff and the radiation line. The partial
		// block damage is NOT here at all: it has no Runtime table, so it rides the
		// native half instead (CaptureMidRunFacts_CarriesTheGamesOwnDamageRows).
		var state = Assert.Single(facts.Blocks).BlockState!;
		Assert.Equal((1, 2), (state.X, state.Y));
		Assert.Equal(2f, Assert.Single(facts.Transients).RadiationLine!.TimeGone);
	}

	[Fact]
	public void CaptureMidRunFacts_WithoutANativeReader_StillCarriesTheRuntimeFacts()
	{
		// The native half is optional by design, and the cut says so rather than
		// pretending the missing half was empty: the Runtime-owned facts still ride.
		using var fixture = WorldSaveFixture.Create("facts-no-native");
		fixture.WorldFacts.SeedBlockState(1, 2, 0);

		var facts = fixture.Writer.CaptureMidRunFacts(WorldCutReason.Command, WorldCutKind.MidRun);

		Assert.Single(facts.Blocks);
	}

	[Fact]
	public void CaptureMidRunFacts_CarriesTheNativeDecidedValues()
	{
		// Keypad codes and geyser liquid types are DECIDED by generation: if the cut
		// does not carry them, a restore rolls new ones and a code the player already
		// read stops opening the door.
		var native = new FakeNativeWorldFacts();
		native.SeedKeypad(1, 2, "1234");
		native.SeedGeyser(3, 4, 2);
		using var fixture = WorldSaveFixture.Create("facts-native-capture", nativeWorldFacts: native);

		var facts = fixture.Writer.CaptureMidRunFacts(WorldCutReason.Command, WorldCutKind.MidRun);

		Assert.Equal("1234", Assert.Single(facts.Transients, row => row.Kind == SaveWorldTransientRow.KeypadKind).Keypad!.Code);
		Assert.Equal(2, Assert.Single(facts.Transients, row => row.Kind == SaveWorldTransientRow.GeyserKind).Geyser!.LiquidType);
	}

	[Fact]
	public void Restore_WithANativeApplier_HandsOverTheKeypadAndGeyserRows()
	{
		using var fixture = WorldSaveFixture.Create("facts-native-round-trip");
		var native = new FakeNativeWorldFacts();
		WriteMidRunSnapshot(
			fixture,
			transients:
			[
				SaveWorldTransientRow.OfKeypad(Keypad(1, 2, "1234")),
				SaveWorldTransientRow.OfGeyser(new GeyserStateEntryMsg { Position = new NetVector2Msg(3f, 4f), LiquidType = 2 }),
			]);

		using var restarted = WorldSaveFixture.Create(
			"facts-native-round-trip-restore",
			repository: fixture.Repository,
			nativeWorldFacts: native);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Equal("1234", Assert.Single(native.Keypads).Code);
		Assert.Equal(2, Assert.Single(native.Geysers).LiquidType);
		Assert.DoesNotContain("not restored", outcome.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void Restore_WithoutANativeApplier_NamesTheGapInTheOutcome()
	{
		using var fixture = WorldSaveFixture.Create("facts-native-gap");
		WriteMidRunSnapshot(
			fixture,
			transients: [SaveWorldTransientRow.OfKeypad(Keypad(1, 2, "1234"))]);

		using var restarted = fixture.Restart("facts-native-gap-restore");

		// §6: an unapplied fact is REPORTED, not only logged — the summary the
		// caller (and the player-facing surface) reads has to carry it.
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);
		Assert.Contains("1 keypad code(s)", outcome.Summary, StringComparison.Ordinal);
		Assert.Contains("not restored", outcome.Summary, StringComparison.Ordinal);
	}

	// ---- restore ----

	[Fact]
	public void TryContinue_AppliesTheRestoredWorldFactsAbsolutely()
	{
		using var fixture = WorldSaveFixture.Create("facts-restore");
		var snapshot = WriteMidRunSnapshot(
			fixture,
			blocks:
			[
				SaveWorldBlockRow.OfBlockState(3, 4, 0),
				SaveWorldBlockRow.OfNativeBlockDamage(5, 6, 0.25f),
			],
			transients: [SaveWorldTransientRow.OfRadiationLine(Radiation(active: true, timeGone: 7f))]);

		var native = new FakeNativeWorldFacts();
		using var restarted = WorldSaveFixture.Create(
			"facts-restore-restart",
			repository: fixture.Repository,
			nativeWorldFacts: native);
		restarted.WorldFacts.SeedBlockState(99, 99, 7); // a fact the cut does not name
		restarted.WorldFacts.RadiationLine = Radiation(active: true, timeGone: 99f);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The apply the save service makes is the WHOLE fact set, and it is one call:
		// the port's own contract is that it resets first (WorldFactLifecycle does),
		// so the save service must never try to merge or pre-clear the tables itself.
		Assert.True(restarted.WorldFacts.IndexOf("apply") >= 0, "the restore never applied the cut's world facts");
		Assert.True(restarted.WorldFacts.IndexOf("capture-blocks") < 0, "a restore must not capture the live tables it is about to replace");

		var state = Assert.Single(restarted.WorldFacts.Blocks);
		Assert.Equal((3, 4), (state.X, state.Y));
		Assert.Equal(0, state.Block);
		Assert.Equal(7f, restarted.WorldFacts.RadiationLine!.TimeGone);

		// The cut's native row went to the game's own list, never to a Runtime table.
		Assert.Equal(0.25f, Assert.Single(native.Damages).Damage);
		Assert.True(snapshot);
	}

	[Fact]
	public void TryContinue_HandsTheNativeTransientFactsToTheAdapter()
	{
		using var fixture = WorldSaveFixture.Create("facts-native-restore");
		WriteMidRunSnapshot(
			fixture,
			transients:
			[
				SaveWorldTransientRow.OfKeypad(Keypad(1, 2, "1234")),
				SaveWorldTransientRow.OfGeyser(new GeyserStateEntryMsg { Position = new NetVector2Msg(3f, 4f), LiquidType = 2 }),
			]);

		var native = new FakeNativeWorldFacts();
		using var restarted = WorldSaveFixture.Create(
			"facts-native-restore-restart",
			repository: fixture.Repository,
			nativeWorldFacts: native);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Contains("apply-keypads", native.Calls);
		Assert.Contains("apply-geysers", native.Calls);
		Assert.Equal("1234", Assert.Single(native.Keypads).Code);
		Assert.Equal(2, Assert.Single(native.Geysers).LiquidType);
	}

	[Fact]
	public void TryContinue_WithoutANativeApplier_StillAppliesTheRuntimeFacts()
	{
		using var fixture = WorldSaveFixture.Create("facts-native-absent");
		WriteMidRunSnapshot(
			fixture,
			blocks: [SaveWorldBlockRow.OfBlockState(3, 4, 0)],
			transients: [SaveWorldTransientRow.OfKeypad(Keypad(1, 2, "1234"))]);

		using var restarted = fixture.Restart("facts-native-absent-restart");

		// The keypad row has nowhere to go without the adapter, but the block diff
		// still applies — the two halves fail independently.
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);
		Assert.Equal((3, 4), (Assert.Single(restarted.WorldFacts.Blocks).X, restarted.WorldFacts.Blocks[0].Y));
	}

	// ---- the game's own damage table (the native half of world-blocks.json) ----

	[Fact]
	public void Codec_RoundTripsTheGameDamageRow()
	{
		// The game's blockDamages list is the ONLY partial-damage table there is, so
		// its rows carry their own kind beside the block diff and round-trip unchanged.
		var (decode, salvage) = RoundTrip(blocks:
		[
			SaveWorldBlockRow.OfBlockState(5, 6, 0),
			SaveWorldBlockRow.OfNativeBlockDamage(7, 8, 2f),
		]);

		Assert.NotNull(decode.Checkpoint);
		Assert.True(salvage.IsClean, salvage.Report.Describe());
		var native = Assert.Single(decode.UsableWorldBlocks, row => row.Kind == SaveWorldBlockRow.NativeBlockDamageKind).NativeBlockDamage!;
		Assert.Equal((7, 8, 2f), (native.X, native.Y, native.Damage));
	}

	[Fact]
	public void Decode_NativeDamageRowMissingItsPayload_IsSkippedWhileTheKnownRowsApply()
	{
		// `damage` is left out: its zero would deserialize into a real-looking
		// "no damage at (0,0)" row, so the row is refused by name and the rest of
		// the file still applies.
		var (decode, salvage) =
			RoundTrip(rawBlocks:
			[
				StateRow(3, 4, 0),
				$"{{\n    \"kind\": \"{SaveWorldBlockRow.NativeBlockDamageKind}\",\n    \"nativeBlockDamage\": {{\n      \"x\": 1,\n      \"y\": 2\n    }}\n  }}",
			]);

		Assert.NotNull(decode.Checkpoint);
		var restoredState = Assert.Single(decode.UsableWorldBlocks);
		Assert.Equal((3, 4), (restoredState.BlockState!.X, restoredState.BlockState.Y));
		Assert.Equal("native-block-damage at (1,2)", Assert.Single(salvage.SkippedEntries).Id);
	}

	[Fact]
	public void CaptureMidRunFacts_CarriesTheGamesOwnDamageRows()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedBlockDamage(7, 8, 2f);
		using var fixture = WorldSaveFixture.Create("facts-native-damage", nativeWorldFacts: native);
		fixture.WorldFacts.SeedBlockState(5, 6, 0);

		var facts = fixture.Writer.CaptureMidRunFacts(WorldCutReason.MenuReturn, WorldCutKind.MidRun);

		Assert.Equal(2, facts.Blocks.Count);
		Assert.Equal((5, 6), (Assert.Single(facts.Blocks, row => row.Kind == SaveWorldBlockRow.BlockStateKind).BlockState!.X, 6));
		Assert.Equal(2f, Assert.Single(facts.Blocks, row => row.Kind == SaveWorldBlockRow.NativeBlockDamageKind).NativeBlockDamage!.Damage);
	}

	[Fact]
	public void Restore_HandsTheGameDamageRowToTheNativeApplier()
	{
		using var fixture = WorldSaveFixture.Create("facts-damage-routing");
		WriteMidRunSnapshot(
			fixture,
			blocks: [SaveWorldBlockRow.OfNativeBlockDamage(7, 8, 2f)]);

		var native = new FakeNativeWorldFacts();
		using var restarted = WorldSaveFixture.Create(
			"facts-damage-routing-restart",
			repository: fixture.Repository,
			nativeWorldFacts: native);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The game's own cell goes to the adapter's table and nowhere else: the
		// Runtime half of the restore has no damage table to receive it.
		Assert.Empty(restarted.WorldFacts.Blocks);
		Assert.Equal((7, 8, 2f), (Assert.Single(native.Damages).X, native.Damages[0].Y, native.Damages[0].Damage));
		Assert.Contains("apply-block-damages", native.Calls);
		Assert.DoesNotContain("not restored", outcome.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void Restore_NamesTheRowsTheBoundedTableRefusedInTheOutcome()
	{
		// §6: reporting success while a row was dropped is forbidden. The block diff
		// is the one Runtime table left with a cap, so the restore's account carries it.
		using var fixture = WorldSaveFixture.Create("facts-refused");
		WriteMidRunSnapshot(
			fixture,
			blocks:
			[
				SaveWorldBlockRow.OfBlockState(3, 4, 0),
				SaveWorldBlockRow.OfNativeBlockDamage(5, 6, 0.5f),
			]);

		using var restarted = fixture.Restart("facts-refused-restart");
		restarted.WorldFacts.RefusedBlockStates = 1;

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Contains("did not land in the table", outcome.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void Restore_LeavesTheLiveWorldReplayPending_AndANewRunDropsIt()
	{
		using var fixture = WorldSaveFixture.Create("facts-replay-pending");
		WriteMidRunSnapshot(fixture, blocks: [SaveWorldBlockRow.OfBlockState(3, 4, 0)]);

		using var restarted = fixture.Restart("facts-replay-pending-restart");

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The restored tables must survive the world-entry reset and be written
		// into the freshly generated layer: the adapter's world-entry hook reads
		// this marker to know that this generation is a RESTORE, not a new layer.
		Assert.True(restarted.WorldFacts.HasPendingLiveReplay, "a restored cut owns the next generation");

		Assert.True(restarted.Service.TryBeginRun());
		Assert.False(restarted.WorldFacts.HasPendingLiveReplay, "a new run must never inherit a previous restore's replay");
		Assert.Contains("clear-pending-replay", restarted.WorldFacts.Calls);
	}

	[Fact]
	public void Restore_WithoutWorldFacts_LeavesNoLiveWorldReplayPending()
	{
		using var fixture = WorldSaveFixture.Create("facts-replay-empty");
		WriteMidRunSnapshot(fixture);

		using var restarted = fixture.Restart("facts-replay-empty-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// A cut that carried no in-layer fact (the layer-end shape) leaves the normal
		// layer lifecycle alone: the next generation resets and generates as usual.
		Assert.False(restarted.WorldFacts.HasPendingLiveReplay);
	}

	// ---- helpers ----

	private static WorldSnapshotEncoder Encoder() => new(NullLogger<WorldSnapshotEncoder>.Instance);

	private static WorldSnapshotPayload Payload(
		WorldCutKind Kind = WorldCutKind.LayerEnd,
		IReadOnlyList<SaveWorldBlockRow>? blocks = null,
		IReadOnlyList<SaveWorldTransientRow>? transients = null) =>
		new(
			WorldSnapshotCodecTests.StartedAuthority(RunId).CreateCheckpoint(),
			[],
			"Test World",
			"command",
			"mid-run",
			WorldBlocks: blocks,
			WorldTransients: transients,
			Kind: Kind);

	private static KeypadEntryMsg Keypad(float x, float y, string code) =>
		new() { Position = new NetVector2Msg(x, y), Code = code };

	private static RadiationLineStateMsg Radiation(bool active, float timeGone) =>
		new() { Active = active, TimeGone = timeGone };

	// The on-disk spelling of the rows (§3.4): indented, camelCase, the kind
	// discriminator beside its payload. Hand-written so the tests pin the format
	// rather than mirroring whatever the writer happens to emit.
	private static string StateRow(int x, int y, ushort block) =>
		$"{{\n    \"kind\": \"{SaveWorldBlockRow.BlockStateKind}\",\n    \"blockState\": {{\n      \"x\": {x},\n      \"y\": {y},\n      \"block\": {block}\n    }}\n  }}";

	private static string DamageRow(int x, int y, float damage) =>
		$"{{\n    \"kind\": \"{SaveWorldBlockRow.NativeBlockDamageKind}\",\n    \"nativeBlockDamage\": {{\n      \"x\": {x},\n      \"y\": {y},\n      \"damage\": {damage.ToString(CultureInfo.InvariantCulture)}\n    }}\n  }}";

	private static string KeypadRow(float x, float y, string code) =>
		$"{{\n    \"kind\": \"{SaveWorldTransientRow.KeypadKind}\",\n    \"keypad\": {{\n      \"position\": {{\n        \"x\": {x.ToString(CultureInfo.InvariantCulture)},\n        \"y\": {y.ToString(CultureInfo.InvariantCulture)}\n      }},\n      \"code\": \"{code}\"\n    }}\n  }}";

	private static string GeyserRow(float x, float y, byte liquidType) =>
		$"{{\n    \"kind\": \"{SaveWorldTransientRow.GeyserKind}\",\n    \"geyser\": {{\n      \"position\": {{\n        \"x\": {x.ToString(CultureInfo.InvariantCulture)},\n        \"y\": {y.ToString(CultureInfo.InvariantCulture)}\n      }},\n      \"liquidType\": {liquidType}\n    }}\n  }}";

	private static string RadiationRow(bool active, float timeGone) =>
		$"{{\n    \"kind\": \"{SaveWorldTransientRow.RadiationLineKind}\",\n    \"radiationLine\": {{\n      \"active\": {(active ? "true" : "false")},\n      \"timeGone\": {timeGone.ToString(CultureInfo.InvariantCulture)}\n    }}\n  }}";

	/// <summary>One encode → write → decode pass, with the two fact files replaceable by raw JSON.</summary>
	private static (WorldSnapshotDecode Decode, SalvageResult Salvage) RoundTrip(
		IReadOnlyList<SaveWorldBlockRow>? blocks = null,
		IReadOnlyList<SaveWorldTransientRow>? transients = null,
		IReadOnlyList<string>? rawBlocks = null,
		IReadOnlyList<string>? rawTransients = null)
	{
		var payload = Payload(
			Kind: WorldCutKind.MidRun,
			blocks: rawBlocks is null ? blocks : null,
			transients: rawTransients is null ? transients : null);
		var files = Encoder().Encode(payload);

		if (rawBlocks is not null)
		{
			files = Replace(files, SaveArchiveFormat.WorldBlocksFileName, ArrayOf(rawBlocks));
		}

		if (rawTransients is not null)
		{
			files = Replace(files, SaveArchiveFormat.WorldTransientsFileName, ArrayOf(rawTransients));
		}

		return WorldSnapshotCodecTests.Decode(files, RunId, WorldCutKind.MidRun);
	}

	private static IReadOnlyList<SavePayloadFile> Replace(IReadOnlyList<SavePayloadFile> files, string path, string content) =>
		[.. files.Where(file => file.Path != path), new SavePayloadFile(path, Encoding.UTF8.GetBytes(content))];

	private static string ArrayOf(IReadOnlyList<string> rows) =>
		"[\n  " + string.Join(",\n  ", rows) + "\n]\n";

	/// <summary>
	/// Writes a real mid-run snapshot of the fixture's world (the run baseline the
	/// kernel holds plus the two fact files) and points the continue pointer at it.
	/// </summary>
	private static bool WriteMidRunSnapshot(
		WorldSaveFixture fixture,
		IReadOnlyList<SaveWorldBlockRow>? blocks = null,
		IReadOnlyList<SaveWorldTransientRow>? transients = null)
	{
		Assert.True(fixture.Service.TryBeginRun());
		Assert.True(fixture.Kernel.TryStartRun(1001UL, WorldSaveCaptureTests.Run(layerIndex: 2), out _, out _));

		var files = Encoder().Encode(Payload(
			Kind: WorldCutKind.MidRun,
			blocks: blocks,
			transients: transients));

		var meta = SaveTestData.Meta(runEpoch: RunId.ToString(CultureInfo.InvariantCulture));
		var write = fixture.Repository.Repository.WriteSnapshot(
			fixture.WorldId,
			SaveTestData.Request(fixture.WorldId, WorldCutKind.MidRun, fixture.Repository.Now, meta, [.. files]));
		Assert.True(write.Success, $"{write.Reason}: {write.Detail}");
		return fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId);
	}

	private static JsonElement[] Entries(IReadOnlyList<SavePayloadFile> files, string path)
	{
		using var document = JsonDocument.Parse(Content(files, path));
		return [.. document.RootElement.EnumerateArray().Select(element => element.Clone())];
	}

	private static byte[] Content(IReadOnlyList<SavePayloadFile> files, string path) =>
		Assert.Single(files, file => file.Path == path).Content;
}
