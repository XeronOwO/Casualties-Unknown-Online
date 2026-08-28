using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Projections;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The three-node item-domain world: a handshaken host + two guests on the
/// shared virtual clock (phase-2 simulation), with the reporters' wire surface
/// recorded — every frame each guest received. Shared by the hand-written
/// item simulations (ItemSimulationTests) and the phase-4 replay runner
/// (ReplayRunner); the replay files drive the same injection helpers the
/// hand-written scenarios use.
/// </summary>
internal sealed class ItemSimWorld : IDisposable
{
	private const ulong HostId = 1001;
	private const ulong G1Id = 2001;
	private const ulong G2Id = 3001;
	private const ulong LobbyId = 9001;

	private readonly List<(NetMsg Msg, byte[] Frame)> _g1Received = [];
	private readonly List<(NetMsg Msg, byte[] Frame)> _g2Received = [];
	private readonly List<ItemRejectMsg> _g1Rejects = [];
	private readonly List<ItemRejectMsg> _g2Rejects = [];

	private ItemSimWorld(SimulationDriver driver, TestNode host, TestNode g1, TestNode g2)
	{
		Driver = driver;
		Host = host;
		G1 = g1;
		G2 = g2;
	}

	internal SimulationDriver Driver { get; }

	internal TestNode Host { get; }

	internal TestNode G1 { get; }

	internal TestNode G2 { get; }

	/// <summary>The host's authoritative world-item table (the arbitration surface).</summary>
	internal ItemService Items => Host.Services.GetRequiredService<ItemService>();

	/// <summary>Resolve a replay-file node alias ("host"/"g1"/"g2").</summary>
	internal TestNode Node(string alias) => alias switch
	{
		"host" => Host,
		"g1" => G1,
		"g2" => G2,
		_ => throw new ArgumentException($"unknown node alias '{alias}' (host/g1/g2)"),
	};

	public void Dispose()
	{
		Host.Dispose();
		G1.Dispose();
		G2.Dispose();
	}

	internal static ItemSimWorld Create()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var g1Steam = new FakeSteamService(G1Id) { LobbyOwner = HostId, LobbyMembers = [HostId, G1Id, G2Id] };
		var g2Steam = new FakeSteamService(G2Id) { LobbyOwner = HostId, LobbyMembers = [HostId, G1Id, G2Id] };
		var host = TestNode.Create(HostId, network, hostSteam, clock);
		var g1 = TestNode.Create(G1Id, network, g1Steam, clock);
		var g2 = TestNode.Create(G2Id, network, g2Steam, clock);
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, G1Id, G2Id];
		g1.Steam.FireLobbyEntered(LobbyId);
		g2.Steam.FireLobbyEntered(LobbyId);
		var driver = new SimulationDriver(clock, network, host, g1, g2);
		driver.TickUntil(
			() => host.Session.Members.Count(m => m.Handshaken) == 2 && g1.Session.Members.Any(m => m.Handshaken) && g2.Session.Members.Any(m => m.Handshaken),
			maxMs: 5000);

		var world = new ItemSimWorld(driver, host, g1, g2);
		g1.Transport.MessageReceived += (_, frame) => world._g1Received.Add(((NetMsg)frame[0], frame));
		g2.Transport.MessageReceived += (_, frame) => world._g2Received.Add(((NetMsg)frame[0], frame));
		g1.Services.GetRequiredService<IItemControl>().ItemRejected += (id, reason) =>
			world._g1Rejects.Add(new ItemRejectMsg { ItemId = id, Rejection = reason });
		g2.Services.GetRequiredService<IItemControl>().ItemRejected += (id, reason) =>
			world._g2Rejects.Add(new ItemRejectMsg { ItemId = id, Rejection = reason });
		return world;
	}

	// ===== Injection helpers (one player operation = one message, the phase-1
	// operation-merge rule) =====

	internal void Spawn(TestNode guest, ulong itemId, CharacterItemMsg item) =>
		Send(guest, NetMsg.ItemSpawn, new ItemSpawnMsg { ItemId = itemId, Item = item });

	internal void Pickup(TestNode guest, ulong itemId, CharacterItemMsg? evidence = null) =>
		Send(guest, NetMsg.ItemPickup, new ItemPickupMsg { ItemId = itemId, Item = evidence });

	/// <summary>A drop report. The position is a real drop spot — NOT the spawn
	/// spot (0,0): the host's duplicate guard is "same itemId + same position +
	/// same rotation" (ItemService.FireItemDroppedReceived), so a drop at the
	/// spawn spot would be mistaken for the spawn report's retransmission and
	/// swallowed (the broadcast would never reach the peers). The shell carries
	/// the semantic "dropped at the player's feet", exactly like the game.</summary>
	internal void Drop(TestNode guest, ulong itemId, CharacterItemMsg item) =>
		Send(guest, NetMsg.ItemDrop, new ItemDropMsg
		{
			ItemId = itemId,
			Item = item,
			Position = new NetVector2Msg { X = 10, Y = 10 },
		});

	internal void Destroy(TestNode guest, ulong itemId) =>
		Send(guest, NetMsg.ItemDestroy, new ItemDestroyMsg { ItemId = itemId });

	internal void Use(TestNode guest, ulong itemId, CharacterItemMsg item) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemUse(itemId, item);

	internal void Slot(TestNode guest, ulong itemId, int slotIndex, CharacterItemMsg item) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemSlot(itemId, slotIndex, item);

	/// <summary>One crafting operation's complete terminal state (the one-operation-one-report convention).</summary>
	internal void Craft(TestNode guest, CraftReportMsg msg) => Send(guest, NetMsg.CraftReport, msg);

	/// <summary>The host's heater conversion (one operation = one ItemCook broadcast).</summary>
	internal void Cook(ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item) =>
		Items.SendItemCooked(sourceItemId, cookedItemId, item, new NetVector2(10, 20), new NetVector2(0, 0), 0f, 0f);

	/// <summary>A blueprint use unlocked a recipe.</summary>
	internal void Unlock(TestNode guest, int recipeIndex) =>
		Send(guest, NetMsg.RecipeUnlock, new RecipeUnlockMsg { RecipeIndex = recipeIndex });

	private static void Send(TestNode guest, NetMsg msg, object payload)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, msg, payload);
	}

	// ===== Wire-surface queries =====

	/// <summary>Whether the host's world-item table currently holds the item.</summary>
	internal bool HostTable(ulong itemId) => Items.IsWorldItemRegistered(itemId);

	/// <summary>Whether the host's transfer table records the item as owned by the guest (the carried-record surface the craft domain mutates).</summary>
	internal bool TransferredOf(TestNode guest, ulong itemId) =>
		Items.GetTransferredItems(guest.SteamId).Any(w => w.Item.InstanceId == itemId);

	/// <summary>The rejects the node has received so far (cumulative — the assertion is "ever received").</summary>
	internal List<ItemRejectMsg> Rejects(TestNode node) =>
		node == G1 ? [.. _g1Rejects] : [.. _g2Rejects];

	/// <summary>The id-watermark grants the node has received so far (cumulative — the rejoin grant is the assertion surface).</summary>
	internal List<ItemIdWatermarkMsg> Watermarks(TestNode node) =>
		[.. Received(node).Where(r => r.Msg == NetMsg.ItemIdWatermark).Select(r => NetPacket.DecodePayload<ItemIdWatermarkMsg>(r.Frame))];

	/// <summary>How many frames of one message type the node has received so far (cumulative).</summary>
	internal int ReceivedCount(TestNode node, NetMsg msg) => Received(node).Count(r => r.Msg == msg);

	/// <summary>How many frames of ANY message type the node has received so far
	/// (cumulative) — the SimTrace "Committed(n)" message-count surface.</summary>
	internal int ReceivedTotal(TestNode node) => Received(node).Count;

	/// <summary>
	/// Semantic diff between the legacy host terminal facts (world table +
	/// transfer table) and the production item kernel shadow. Revision is not
	/// comparable yet because the legacy path has no aggregate revisions.
	/// </summary>
	internal ItemTerminalDiff CompareKernelShadow() =>
		ItemDiagnosticsProjection.Compare(
			BuildLegacyActiveFacts(),
			ItemDiagnosticsProjection.BuildActiveFacts(Items.KernelShadow.KernelForDiagnostics.QueryItems().Values),
			includeRevision: false);

	private IReadOnlyDictionary<ulong, ItemTerminalFact> BuildLegacyActiveFacts()
	{
		var facts = new Dictionary<ulong, ItemTerminalFact>();
		foreach (var worldItem in Items.GetWorldItemsForDiagnostics())
		{
			facts[worldItem.ItemId] = new ItemTerminalFact(
				worldItem.ItemId,
				worldItem.Item.ItemId,
				ItemLocationKind.World,
				0,
				worldItem.ParentItemId,
				worldItem.Pos.X,
				worldItem.Pos.Y,
				0);
		}

		foreach (var guest in new[] { G1, G2 })
		{
			foreach (var transferred in Items.GetTransferredItems(guest.SteamId))
			{
				facts[transferred.ItemId] = new ItemTerminalFact(
					transferred.ItemId,
					transferred.Item.ItemId,
					ItemLocationKind.Carried,
					guest.SteamId,
					0,
					0,
					0,
					0);
			}
		}

		return facts;
	}

	private List<(NetMsg Msg, byte[] Frame)> Received(TestNode node)
	{
		if (node == G1)
		{
			return _g1Received;
		}

		if (node == G2)
		{
			return _g2Received;
		}

		throw new ArgumentException("only g1/g2 wire surfaces are recorded", nameof(node));
	}
}
