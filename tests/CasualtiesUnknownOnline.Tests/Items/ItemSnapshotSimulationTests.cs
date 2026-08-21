using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The world-item snapshot surface (host → guest): the full-table snapshot on
/// world entry (the late joiner / reconnect receives the authoritative table),
/// the periodic unreliable keyframe (the drift self-heal), and the
/// generation-time item publish (ground + starting supplies, one broadcast).
/// The layer-modifier projection rides every snapshot (wire encoding
/// modifierIndex + 1 — Foggy's raw index IS 0, see ItemSnapshotService).
/// </summary>
public class ItemSnapshotSimulationTests
{
	private static CharacterItemMsg Item(string id) => new() { ItemId = id, Condition = 1f };

	[Fact]
	public void WorldEntrySnapshot_CarriesEveryWorldItem()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 100, Item("ore"));
		w.Spawn(w.G1, 200, Item("water"));

		var received = new List<IReadOnlyList<WorldItem>>();
		w.G2.Services.GetRequiredService<IItemControl>().ItemSnapshotReceived += (items, _, _) => received.Add(items);

		w.Host.Services.GetRequiredService<IItemControl>().SendItemSnapshot(w.G2.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1, $"the world-entry snapshot must arrive, got {received.Count}");
		Assert.True(received[0].Count == 2, $"every table item rides the snapshot, got {received[0].Count}");
		Assert.Contains(received[0], i => i.ItemId == 100);
		Assert.Contains(received[0], i => i.ItemId == 200);
	}

	[Fact]
	public void WorldEntrySnapshot_EmptyTable_SendsNothing()
	{
		using var w = ItemSimWorld.Create();

		var received = new List<IReadOnlyList<WorldItem>>();
		w.G1.Services.GetRequiredService<IItemControl>().ItemSnapshotReceived += (items, _, _) => received.Add(items);

		w.Host.Services.GetRequiredService<IItemControl>().SendItemSnapshot(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 0, $"an empty table sends no snapshot, got {received.Count}");
	}

	[Fact]
	public void WorldEntrySnapshot_LayerModifierWirePlusOneRoundTrips()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 100, Item("ore"));

		var modifiers = new List<int>();
		w.G2.Services.GetRequiredService<IItemControl>().ItemSnapshotReceived += (_, modifierIndex, _) => modifiers.Add(modifierIndex);

		// Foggy's raw index is 0 — the wire encodes +1 so protobuf-net's
		// 0-omission cannot swallow it (see ItemSnapshotService.SendItemSnapshot).
		w.Host.Services.GetRequiredService<IItemControl>().LayerModifierIndex = 0;
		w.Host.Services.GetRequiredService<IItemControl>().SendItemSnapshot(w.G2.SteamId);
		w.Host.Services.GetRequiredService<IItemControl>().LayerModifierIndex = 3;
		w.Host.Services.GetRequiredService<IItemControl>().SendItemSnapshot(w.G2.SteamId);
		w.Driver.Tick(50);

		Assert.True(modifiers.Count == 2, $"both snapshots must arrive, got {modifiers.Count}");
		Assert.True(modifiers[0] == 1, $"Foggy (0) rides as 1, got {modifiers[0]}");
		Assert.True(modifiers[1] == 4, $"index 3 rides as 4, got {modifiers[1]}");
	}

	[Fact]
	public void WorldEntrySnapshot_RandomStateRidesAlong()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 100, Item("ore"));

		var states = new List<byte[]?>();
		w.G2.Services.GetRequiredService<IItemControl>().ItemSnapshotReceived += (_, _, randomState) => states.Add(randomState);

		w.Items.LayerModifierRandomState = [7, 42];
		w.Host.Services.GetRequiredService<IItemControl>().SendItemSnapshot(w.G2.SteamId);
		w.Driver.Tick(50);

		Assert.True(states.Count == 1, $"the snapshot must arrive, got {states.Count}");
		Assert.True(states[0] is { Length: 2 } s && s[0] == 7 && s[1] == 42, "the random state bytes ride unchanged");
	}

	[Fact]
	public void PeriodicSnapshot_ReachesEveryHandshakenMember()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 100, Item("ore"));

		w.Host.Services.GetRequiredService<IItemControl>().SendPeriodicItemSnapshot();
		w.Driver.Tick(50);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.ItemSnapshot) == 1, $"g1 must get the keyframe, got {w.ReceivedCount(w.G1, NetMsg.ItemSnapshot)}");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.ItemSnapshot) == 1, $"g2 must get the keyframe, got {w.ReceivedCount(w.G2, NetMsg.ItemSnapshot)}");
	}

	[Fact]
	public void PeriodicSnapshot_EmptyTable_SendsNothing()
	{
		using var w = ItemSimWorld.Create();

		w.Host.Services.GetRequiredService<IItemControl>().SendPeriodicItemSnapshot();
		w.Driver.Tick(50);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.ItemSnapshot) == 0 && w.ReceivedCount(w.G2, NetMsg.ItemSnapshot) == 0,
			"an empty table broadcasts no keyframe");
	}

	[Fact]
	public void PeriodicSnapshot_CarriesTopLevelComponentAndLiquidState()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 100, new CharacterItemMsg
		{
			ItemId = "waterbottle",
			Condition = 0.8f,
			Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = 0.4f }],
			Components =
			[
				new ComponentStateMsg
				{
					TypeName = "CustomItemBehaviour",
					Fields = [new ComponentFieldMsg { Name = "state", Kind = 2, IntValue = 2 }],
				},
			],
		});

		var received = new List<IReadOnlyList<WorldItem>>();
		w.G2.Services.GetRequiredService<IItemControl>().ItemSnapshotReceived += (items, _, _) => received.Add(items);

		w.Host.Services.GetRequiredService<IItemControl>().SendPeriodicItemSnapshot();
		w.Driver.Tick(50);

		Assert.True(received.Count == 1, $"the keyframe must arrive, got {received.Count}");
		var entry = received[0].Single(i => i.ItemId == 100);
		Assert.True(entry.Item.Liquids.Count == 1 && entry.Item.Liquids[0].Amount == 0.4f,
			"the keyframe carries the current liquid stacks");
		Assert.True(entry.Item.Components.Count == 1
			&& entry.Item.Components[0].Fields.Single(f => f.Name == "state").IntValue == 2,
			"the keyframe carries the current component state");
	}

	[Fact]
	public void GenerationSnapshot_PublishBroadcastsRawEntriesWithModifier()
	{
		using var w = ItemSimWorld.Create();

		var received = new List<WorldItemsSnapshotMsg>();
		w.G1.Services.GetRequiredService<IItemControl>().WorldItemsSnapshotReceived += (entries, modifierIndex, randomState) =>
			received.Add(new WorldItemsSnapshotMsg { Items = [.. entries], LayerModifierIndex = modifierIndex, LayerModifierRandomState = randomState });

		w.Host.Services.GetRequiredService<IItemControl>().LayerModifierIndex = 0; // Foggy
		w.Host.Services.GetRequiredService<IItemControl>().PublishGeneratedItems(
		[
			new ItemSnapshotEntryMsg { ItemId = 1, Item = Item("ore"), SlotIndex = 0 }, // ground
			new ItemSnapshotEntryMsg { ItemId = 2, Item = Item("medkit"), SlotIndex = 3 }, // starting supply (wire slot 3 = backpack slot 2)
		]);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1, $"the generation snapshot must arrive, got {received.Count}");
		Assert.True(received[0].Items.Count == 2, $"both entries ride the broadcast, got {received[0].Items.Count}");
		Assert.True(received[0].LayerModifierIndex == 1, $"Foggy (0) rides as 1, got {received[0].LayerModifierIndex}");
	}

	[Fact]
	public void GenerationSnapshot_CarriedEntryStaysOutOfTheWorldTable()
	{
		using var w = ItemSimWorld.Create();

		w.Host.Services.GetRequiredService<IItemControl>().PublishGeneratedItems(
		[
			new ItemSnapshotEntryMsg { ItemId = 1, Item = Item("ore"), SlotIndex = 0 }, // ground — registered
			new ItemSnapshotEntryMsg { ItemId = 2, Item = Item("medkit"), SlotIndex = 3 }, // carried — NO table entry (it lives in a backpack until a drop)
		]);
		w.Driver.Tick(50);

		// The world-entry snapshot is the table's truth: the ground item rides,
		// the carried one does not (ItemService.PublishGeneratedItems skips it).
		var received = new List<IReadOnlyList<WorldItem>>();
		w.G2.Services.GetRequiredService<IItemControl>().ItemSnapshotReceived += (items, _, _) => received.Add(items);
		w.Host.Services.GetRequiredService<IItemControl>().SendItemSnapshot(w.G2.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1, $"the snapshot must arrive, got {received.Count}");
		Assert.True(received[0].Count == 1 && received[0][0].ItemId == 1,
			$"only the ground item is a world item, got [{string.Join(",", received[0].Select(i => i.ItemId))}]");
		Assert.True(w.HostTable(1), "the ground item registered");
		Assert.True(!w.HostTable(2), "the carried item never entered the world table");
	}
}
