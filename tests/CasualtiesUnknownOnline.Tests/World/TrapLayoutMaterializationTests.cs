using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The trap-layout materialization dedup judgment: one entity can produce
/// several layout entries (a turret's two kinds, a mine's two kinds) at the same
/// prefab and position, and materializing per entry would instantiate a
/// duplicate copy — with the SAME creation key once the entries carry it.
/// </summary>
public class TrapLayoutMaterializationTests
{
	private static TrapLayoutEntryMsg Entry(EntityEventKind kind, string prefab, float x, float y, RuntimeEntityKeyMsg? key = null) =>
		new() { Kind = kind, PrefabName = prefab, X = x, Y = y, CreationKey = key };

	private static RuntimeEntityKeyMsg Key(string id, int x, int y, uint sequence) => new()
	{
		Id = id,
		CellX = x,
		CellY = y,
		CreatorSteamId = 2001,
		CreationSequence = sequence,
	};

	[Fact]
	public void Deduplicate_TwoKindsOfOneEntity_CollapsesToOne()
	{
		// A mine is both MinePressed and MineExploded at the same spot.
		var entries = new List<TrapLayoutEntryMsg>
		{
			Entry(EntityEventKind.MinePressed, "mine", 4f, 5f),
			Entry(EntityEventKind.MineExploded, "mine", 4f, 5f),
		};

		var result = TrapLayoutMaterialization.Deduplicate(entries);

		var single = Assert.Single(result.Entries);
		Assert.Equal(EntityEventKind.MinePressed, single.Kind); // the first wins when neither carries a key
		Assert.Equal(1, result.CollapsedCount);
	}

	[Fact]
	public void Deduplicate_KeyedSibling_ReplacesTheKeylessEntry()
	{
		// The keyed entry must be the one materialized: only it can stamp the
		// copy's creation identity for the runtime-entity snapshot.
		var key = Key("mine", 4, 5, 7);
		var entries = new List<TrapLayoutEntryMsg>
		{
			Entry(EntityEventKind.MinePressed, "mine", 4f, 5f),
			Entry(EntityEventKind.MineExploded, "mine", 4f, 5f, key),
		};

		var result = TrapLayoutMaterialization.Deduplicate(entries);

		var single = Assert.Single(result.Entries);
		Assert.Equal(EntityEventKind.MineExploded, single.Kind);
		Assert.Equal(key, single.CreationKey);
		Assert.Equal(1, result.CollapsedCount);
	}

	[Fact]
	public void Deduplicate_DistinctEntities_AreKept()
	{
		var entries = new List<TrapLayoutEntryMsg>
		{
			Entry(EntityEventKind.MinePressed, "mine", 4f, 5f),
			Entry(EntityEventKind.MinePressed, "mine", 6f, 5f), // another mine
			Entry(EntityEventKind.MinePressed, "beartrap", 4f, 5f), // another prefab
		};

		var result = TrapLayoutMaterialization.Deduplicate(entries);

		Assert.Equal(3, result.Entries.Count);
		Assert.Equal(0, result.CollapsedCount);
	}

	[Fact]
	public void Deduplicate_Empty_ProducesNothing() =>
		Assert.Empty(TrapLayoutMaterialization.Deduplicate([]).Entries);
}
