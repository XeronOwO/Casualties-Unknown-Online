using System;
using System.Globalization;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The domain round-trip the retired <c>KernelSaveFileStore</c> used to provide,
/// re-pointed at the REAL save path: the archive encoder, the §5 write
/// transaction, the manifest gate and the per-entry decoder. It is strictly
/// stronger evidence than the old single protobuf blob — the domain tests now
/// exercise the path the game ships, not a store nothing constructed.
///
/// Every domain test that asserts "this fact survives a save/load" goes through
/// here, so the archive's file set, its checksums and its decode contract stay
/// covered by the domain suites themselves.
/// </summary>
internal static class WorldArchiveRoundTrip
{
	/// <summary>Writes the authority's current checkpoint into a fresh archive and decodes it back.</summary>
	internal static GameCheckpoint ThroughArchive(ItemKernelAuthority authority, string label = "domain-round-trip")
	{
		// The format refuses a snapshot without a run baseline (§3.4): without one
		// the layer a restore would generate is unknowable, and guessing it is the
		// silent restart §6 forbids.
		if (authority.QueryRun() is null)
		{
			Assert.True(
				authority.TryStartRun(1UL, new RunState(1UL, [1, 2, 3, 4, 5, 6, 7, 8], 0, 0, 0, false), out _, out _),
				"the archive round-trip needs a run baseline");
		}

		return ThroughArchive(authority.CreateCheckpoint(), label);
	}

	/// <summary>
	/// The checkpoint-level entry: domain tests that drive a raw
	/// <c>GameStateKernel</c> hand their checkpoint in directly. A checkpoint with
	/// no run baseline gets a synthetic one — the archive refuses a run-less
	/// snapshot, and those tests are about their own domain facts, not the run.
	/// </summary>
	internal static GameCheckpoint ThroughArchive(GameCheckpoint checkpoint, string label = "domain-round-trip")
	{
		var payloadCheckpoint = checkpoint.Run is null
			? checkpoint with { Run = new RunState(checkpoint.RunEpoch.Value, [1, 2, 3, 4, 5, 6, 7, 8], 0, 0, 0, false) }
			: checkpoint;

		var test = SaveTestRepository.Create(label);
		var files = new WorldSnapshotEncoder(NullLogger<WorldSnapshotEncoder>.Instance)
			.Encode(new WorldSnapshotPayload(payloadCheckpoint, [], "round-trip", "layer-advance", "layer-boundary"));

		var write = test.Repository.WriteSnapshot(
			test.WorldId,
			SaveTestData.Request(test.WorldId, WorldCutKind.LayerEnd, test.Now, MetaOf(payloadCheckpoint), [.. files]));
		Assert.True(write.Success, $"{write.Reason}: {write.Detail}");

		// VerifyChecksums is what the restore path uses: the payload's bytes are
		// re-read and compared against the manifest before they are applied.
		var options = new WorldLoadOptions { VerifyChecksums = true };
		var load = test.Repository.LoadSnapshot(test.WorldId, options);
		Assert.NotNull(load.Content);

		var decoder = new WorldSnapshotDecoder(load.Content!.Manifest, NullLogger<WorldSnapshotDecoder>.Instance);
		var (_, salvage) = test.Repository.ReadSalvage(load, decoder.DecodeEntry, options);
		Assert.True(salvage.IsClean, salvage.Report.Describe());

		var decode = decoder.Finish();
		Assert.NotNull(decode.Checkpoint);
		return decode.Checkpoint!;
	}

	/// <summary>The manifest provenance of a checkpoint: the cut's own run epoch, revision and layer.</summary>
	internal static SaveManifestMeta MetaOf(GameCheckpoint checkpoint)
	{
		var run = checkpoint.Run!;
		return new SaveManifestMeta
		{
			DisplayName = "round-trip",
			GameBuild = "test",
			CuoBuild = "test",
			ProtocolVersion = 1,
			RunEpoch = checkpoint.RunEpoch.Value.ToString(CultureInfo.InvariantCulture),
			GlobalRevision = (long)checkpoint.GlobalRevision,
			LayerIndex = run.LayerIndex,
			BiomeDepth = run.BiomeDepth,
			PlayerCount = 0,
			CutPhase = "layer-boundary",
			SaveReason = "layer-advance",
		};
	}

	/// <summary>
	/// A stable fingerprint of the restored item facts: identity, location kind and
	/// revision of every item, in instance-id order. Two restores of one snapshot
	/// must produce the same string — acceptance row 4's "idempotent, stable
	/// fingerprint".
	/// </summary>
	internal static string ItemFingerprint(GameCheckpoint checkpoint) =>
		string.Join(";", checkpoint.Items
			.OrderBy(item => item.Identity.InstanceId)
			.Select(item => FormattableString.Invariant(
				$"{item.Identity.InstanceId}:{item.Identity.DefinitionId}:{item.Location.Kind}:{item.Revision}:{item.Data.Condition:R}")));
}
