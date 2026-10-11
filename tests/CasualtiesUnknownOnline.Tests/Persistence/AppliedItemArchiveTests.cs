using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// An APPLIED item (one the game turned into a limb component) across the three
/// places its location travels: the archive's <c>items.json</c>, the host-side
/// restore, and the peer projection. The item keeps its instance id throughout —
/// that is the ruling this family implements — so nothing here may materialize it
/// as a world object, drop it at a layer boundary, or hand a peer a clone it can
/// pick up.
/// </summary>
public class AppliedItemArchiveTests
{
	private const ulong ItemId = 100;
	private const int LimbIndex = 4;
	private const ulong RunId = 42;

	private static readonly ActorId Guest = new(2001);

	[Fact]
	public void MidRunCut_RoundTripsAnAppliedItemRowWithItsOwnerAndLimb()
	{
		var authority = WorldSnapshotCodecTests.StartedAuthority();
		ApplyToLimb(authority);

		var (decoded, salvage) = WorldSnapshotCodecTests.MidRunCodecRoundTrip(authority.CreateCheckpoint());

		Assert.True(salvage.IsClean, salvage.Report.Describe());
		var item = Assert.Single(decoded.Items);
		Assert.Equal(ItemLocationKind.Applied, item.Location.Kind);
		Assert.Equal(Guest, item.Location.Owner);
		Assert.Equal(LimbIndex, item.Location.LimbIndex);
		Assert.Equal(ItemId, item.Identity.InstanceId);
		Assert.Equal("splint", item.Identity.DefinitionId);
	}

	[Fact]
	public void LayerEndCut_WritesTheAppliedRowAndStillDropsTheWorldRootedOne()
	{
		// A layer boundary takes the ground and everything on it. An item held by a
		// limb crosses it with the body, so its row belongs in the cut — and the
		// world item next to it must still be dropped.
		var authority = WorldSnapshotCodecTests.StartedAuthority();
		ApplyToLimb(authority);
		WorldSnapshotCodecTests.Spawn(authority, 200, "shell", ItemLocation.World(1f, 2f));

		var files = WorldSnapshotCodecTests.Encoder().Encode(WorldSnapshotCodecTests.Payload(authority));

		var row = Assert.Single(Entries(files, SaveArchiveFormat.ItemsFileName));
		Assert.Equal(ItemId, row.GetProperty("identity").GetProperty("instanceId").GetUInt64());
		Assert.Equal((int)WireItemLocationKind.Applied, row.GetProperty("location").GetProperty("kind").GetInt32());
		Assert.Equal(Guest.Value, row.GetProperty("location").GetProperty("owner").GetUInt64());
		Assert.Equal(LimbIndex, row.GetProperty("location").GetProperty("limbIndex").GetInt32());
	}

	[Fact]
	public void AppliedRowWithoutALimb_IsSkippedByNameByTheRestoreInsteadOfLandingOnTheHead()
	{
		// The limb is what places the item, and limb 0 is the head, so a row that
		// does not name one must not be read as "the head": the archive's per-entry
		// salvage skips it and names it, and the rest of the domain still applies.
		var authority = WorldSnapshotCodecTests.StartedAuthority();
		ApplyToLimb(authority);
		var files = WorldSnapshotCodecTests.Encoder()
			.Encode(WorldSnapshotCodecTests.MidRunPayload(authority))
			.Select(file => file.Path == SaveArchiveFormat.ItemsFileName
				? new SavePayloadFile(file.Path, WithoutTheLimb(file.Content))
				: file)
			.ToList();

		var (decode, salvage) = WorldSnapshotCodecTests.Decode(files, runEpoch: RunId, kind: WorldCutKind.MidRun);

		Assert.NotNull(decode.Checkpoint);
		Assert.Empty(decode.Checkpoint!.Items);
		Assert.False(salvage.IsClean, "a row the restore could not place is a named skip, never a silent one");
	}

	[Fact]
	public void RestoredAppliedRow_IsNotMaterializedAsAWorldItemAndRaisesNoSpawn()
	{
		var authority = WorldSnapshotCodecTests.StartedAuthority();
		ApplyToLimb(authority);
		var checkpoint = authority.CreateCheckpoint();

		var table = new WorldItemTable();
		var spawned = new List<WorldItem>();
		var projection = new KernelBatchItemProjection(authority, table, spawned.Add, _ => { }, (_, _, _, _, _, _, _, _) => { }, _ => { });
		var restored = new RestoredWorldItemSet(authority, null, NullLogger<RestoredWorldItemSet>.Instance);

		restored.ArmForRestoredCut(checkpoint, projection);

		Assert.Empty(restored.Read());
		Assert.False(table.ContainsKey(ItemId));
		Assert.Empty(spawned);
	}

	[Fact]
	public void ApplyingAnItem_EmitsNoCarriedFactAndNoWorldCloneToAPeer()
	{
		// A peer's clone of the item converges from the owner's 1 Hz character
		// snapshot, the same way a consumed item's does: the apply is not a carried
		// fact, and it must not become a pickable world object either.
		var authority = WorldSnapshotCodecTests.StartedAuthority();
		var table = new WorldItemTable();
		var spawned = new List<WorldItem>();
		var pickedUp = new List<ulong>();
		var carried = new List<ulong>();
		var projection = new KernelBatchItemProjection(
			authority,
			table,
			spawned.Add,
			pickedUp.Add,
			(_, _, _, _, _, _, _, _) => { },
			_ => { },
			(_, item, _) => carried.Add(item.InstanceId));

		Assert.True(authority.TrySpawn(
			Guest.Value,
			new ItemIdentity(ItemId, "splint"),
			ItemLocation.Carried(Guest),
			new CharacterItemMsg { InstanceId = ItemId, ItemId = "splint", Condition = 1f },
			out var spawnBatch,
			out _));
		projection.Apply(spawnBatch!);

		// The control: a CARRIED item is a carried fact on this very seam, so the
		// empty assertion below is about the apply and not about a dead callback.
		Assert.Equal(new ulong[] { ItemId }, carried);

		carried.Clear();
		Assert.True(authority.TryExecuteCommand(ApplyCommand(authority), Guest.Value, out var applyBatch, out var rejection), rejection?.Message);
		projection.Apply(applyBatch!);

		Assert.Empty(carried);
		Assert.Empty(spawned);
		Assert.Empty(pickedUp);
		Assert.False(table.ContainsKey(ItemId));
	}

	private static void ApplyToLimb(ItemKernelAuthority authority)
	{
		WorldSnapshotCodecTests.Spawn(authority, ItemId, "splint", ItemLocation.Carried(Guest));
		Assert.True(
			authority.TryExecuteCommand(ApplyCommand(authority), Guest.Value, out _, out var rejection),
			rejection?.Message);
	}

	private static ApplyLimbItemCommand ApplyCommand(ItemKernelAuthority authority) =>
		new(
			new OperationId(50),
			Guest,
			authority.CurrentRunEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			ItemId,
			Guest,
			LimbIndex,
			1);

	/// <summary>The encoded <c>items.json</c> with the applied row's limb property REMOVED (absent, not zero).</summary>
	private static byte[] WithoutTheLimb(byte[] content)
	{
		var rows = JsonNode.Parse(Encoding.UTF8.GetString(content))!.AsArray();
		rows[0]!["location"]!.AsObject().Remove("limbIndex");
		return Encoding.UTF8.GetBytes(rows.ToJsonString());
	}

	private static IReadOnlyList<JsonElement> Entries(IReadOnlyList<SavePayloadFile> files, string path)
	{
		using var document = JsonDocument.Parse(Assert.Single(files, file => file.Path == path).Content);
		return [.. document.RootElement.EnumerateArray().Select(entry => entry.Clone())];
	}
}
