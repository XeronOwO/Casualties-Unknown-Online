using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Tests.GameState;
using CasualtiesUnknownOnline.GameState.Projections;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
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
	private readonly List<WorldItem> _g1Spawned = [];
	private readonly List<WorldItem> _g2Spawned = [];
	private readonly List<ulong> _g1PickedUp = [];
	private readonly List<ulong> _g2PickedUp = [];
	private readonly List<ulong> _g1Dropped = [];
	private readonly List<ulong> _g2Dropped = [];
	private readonly List<ulong> _g1Destroyed = [];
	private readonly List<ulong> _g2Destroyed = [];
	private readonly List<(ulong Owner, CharacterItemMsg Item)> _g1Carried = [];
	private readonly List<(ulong Owner, CharacterItemMsg Item)> _g2Carried = [];

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

	/// <summary>
	/// Set a guest's scene state on the HOST's presence table — the fact every
	/// host-side world-item stream target rule reads. The harness never runs the
	/// game's scene transition, so tests declare the state they exercise.
	/// </summary>
	internal void SetInWorld(TestNode node, bool inWorld)
	{
		foreach (var member in Host.Session.Members)
		{
			if (member.SteamId == node.SteamId)
			{
				member.InWorld = inWorld;
			}
		}
	}

	public void Dispose()
	{
		Host.Dispose();
		G1.Dispose();
		G2.Dispose();
	}

	/// <summary>
	/// The three nodes on the shared virtual clock. The account ids are parameters
	/// because an item id keys its owner's space by the LOW 32 BITS of the account id
	/// (<c>ItemInstanceIds</c>): a fixture whose ids fit in 32 bits cannot tell "the id
	/// space" from "the full SteamId", so a suite about whose counter an id came from
	/// has to hand in account-sized ones. The defaults are the values the older suites
	/// were written with.
	/// </summary>
	internal static ItemSimWorld Create(
		Action<IServiceCollection>? extraRegistrations = null,
		ulong hostSteamId = HostId,
		ulong guest1SteamId = G1Id,
		ulong guest2SteamId = G2Id,
		string? savesRoot = null)
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(hostSteamId) { LobbyOwner = hostSteamId, LobbyMembers = [hostSteamId] };
		var g1Steam = new FakeSteamService(guest1SteamId) { LobbyOwner = hostSteamId, LobbyMembers = [hostSteamId, guest1SteamId, guest2SteamId] };
		var g2Steam = new FakeSteamService(guest2SteamId) { LobbyOwner = hostSteamId, LobbyMembers = [hostSteamId, guest1SteamId, guest2SteamId] };
		var host = TestNode.Create(hostSteamId, network, hostSteam, clock, extraRegistrations: extraRegistrations, savesRoot: savesRoot);
		var g1 = TestNode.Create(guest1SteamId, network, g1Steam, clock, extraRegistrations: extraRegistrations, savesRoot: savesRoot);
		var g2 = TestNode.Create(guest2SteamId, network, g2Steam, clock, extraRegistrations: extraRegistrations, savesRoot: savesRoot);
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [hostSteamId, guest1SteamId, guest2SteamId];
		g1.Steam.FireLobbyEntered(LobbyId);
		g2.Steam.FireLobbyEntered(LobbyId);
		var driver = new SimulationDriver(clock, network, host, g1, g2);
		driver.TickUntil(
			() => host.Session.Members.Count(m => m.Handshaken) == 2 && g1.Session.Members.Any(m => m.Handshaken) && g2.Session.Members.Any(m => m.Handshaken),
			maxMs: 5000);

		var world = new ItemSimWorld(driver, host, g1, g2);
		g1.Transport.MessageReceived += (_, frame) => world._g1Received.Add(((NetMsg)frame[0], frame));
		g2.Transport.MessageReceived += (_, frame) => world._g2Received.Add(((NetMsg)frame[0], frame));
		BindGuestEvents(world, g1, world._g1Spawned, world._g1PickedUp, world._g1Dropped, world._g1Destroyed, world._g1Carried, world._g1Rejects);
		BindGuestEvents(world, g2, world._g2Spawned, world._g2PickedUp, world._g2Dropped, world._g2Destroyed, world._g2Carried, world._g2Rejects);
		return world;
	}

	private static void BindGuestEvents(
		ItemSimWorld world,
		TestNode node,
		List<WorldItem> spawned,
		List<ulong> pickedUp,
		List<ulong> dropped,
		List<ulong> destroyed,
		List<(ulong Owner, CharacterItemMsg Item)> carried,
		List<ItemRejectMsg> rejects)
	{
		var items = node.Services.GetRequiredService<IItemControl>();
		items.ItemSpawned += item => spawned.Add(item);
		items.ItemPickedUp += id => pickedUp.Add(id);
		items.ItemDropped += (id, _, _, _, _, _, _, _) => dropped.Add(id);
		items.ItemDestroyed += id => destroyed.Add(id);
		items.ItemCarriedSyncReceived += (owner, item, _) => carried.Add((owner, item));
		items.ItemRejected += (id, reason) => rejects.Add(new ItemRejectMsg { ItemId = id, Rejection = reason });
	}

	// ===== Injection helpers (one player operation = one message, the phase-1
	// operation-merge rule) =====

	internal void Spawn(TestNode guest, ulong itemId, CharacterItemMsg item) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemSpawned(itemId, item, new NetVector2(0, 0), new NetVector2(0, 0), 0f, false, 0f);

	internal void Pickup(TestNode guest, ulong itemId, CharacterItemMsg? evidence = null) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemPickedUp(itemId, evidence);

	/// <summary>A drop report. The position is a real drop spot — NOT the spawn
	/// spot (0,0): the host's duplicate guard is "same itemId + same position +
	/// same rotation" (ItemService.FireItemDroppedReceived), so a drop at the
	/// spawn spot would be mistaken for the spawn report's retransmission and
	/// swallowed (the broadcast would never reach the peers). The shell carries
	/// the semantic "dropped at the player's feet", exactly like the game.</summary>
	internal void Drop(TestNode guest, ulong itemId, CharacterItemMsg item) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemDropped(
			itemId, item, new NetVector2(10, 10), new NetVector2(0, 0), 0, 0f);

	internal void Destroy(TestNode guest, ulong itemId) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemDestroyed(itemId);

	/// <summary>The item left a container into the guest's own carried inventory (the arrival half of a cancelled container departure).</summary>
	internal void TakeOut(TestNode guest, ulong itemId, CharacterItemMsg? evidence = null) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemContainerTakeOut(itemId, evidence);

	internal void Use(TestNode guest, ulong itemId, CharacterItemMsg item) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemUse(itemId, item);

	/// <summary>The guest's absolute carried-set statement (<c>NetMsg.CarriedInventory</c>) — the one creation claim the host judges for what that member carries.</summary>
	internal void CarriedSet(TestNode guest, params CharacterItemMsg[] items) =>
		guest.Services.GetRequiredService<IItemControl>().SendCarriedInventory(items);

	/// <summary>The guest's own container-content report, through the PRODUCT's sender, which derives every listed child's parent from the tree (so an honest report can never name another row).</summary>
	internal void ContainerSync(TestNode guest, ulong itemId, CharacterItemMsg parent) =>
		guest.Services.GetRequiredService<IItemControl>().SendItemContainerContent(itemId, parent);

	/// <summary>
	/// A container report whose listed child names a parent the tree does NOT put it under — the
	/// shape only a modified client sends, because the product's sender derives each child's parent
	/// from the tree. It goes out as a wire command through the same control the product's own
	/// sender uses, so what the host judges is an ordinary <c>ItemContainerSync</c> frame.
	/// </summary>
	internal void ForgeContainerChildParent(TestNode guest, ulong itemId, string definitionId, ulong childId, string childDefinitionId, ulong childParentId) =>
		guest.Services.GetRequiredService<IKernelProtocolControl>().SendCommand(
			new WireCommand
			{
				Kind = WireCommandKind.ItemContainerSync,
				Identity = new WireItemIdentity { InstanceId = itemId, DefinitionId = definitionId },
				ContainerChildren =
				[
					new WireContainerChild
					{
						Identity = new WireItemIdentity { InstanceId = childId, DefinitionId = childDefinitionId },
						ParentItemId = childParentId,
					},
				],
			},
			WirePayloadType.ItemContainerSyncCommand);

	/// <summary>Whether the guest's carried-registration window wants a report now — the way back a refused use report arms.</summary>
	internal bool RegistrationDue(TestNode guest) =>
		guest.Services.GetRequiredService<IItemControl>().IsCarriedInventoryRegistrationDue();

	/// <summary>The host's kernel row for an id, or null when the host never judged it.</summary>
	internal ItemState? KernelRow(ulong itemId) =>
		Host.Services.GetRequiredService<ItemKernelAuthority>().FindItem(itemId);

	/// <summary>
	/// The host's cheat-roster grant for one guest — the WHO half of the two cheat
	/// rulings a member's console verb needs (the switch is the other). Throws with the
	/// host's own refusal when it refuses (no session, IP-direct, not a handshaken
	/// member), so a setup error names its ground instead of returning a bare false.
	/// It does NOT announce: the switch-opening and announcement belong to the case.
	/// </summary>
	internal bool GrantCheatVerbs(TestNode guest)
	{
		if (!Host.Services.GetRequiredService<CheatPermissions>().Grant(guest.SteamId, out var refusal))
		{
			throw new InvalidOperationException($"the host refused the cheat grant: {refusal}");
		}

		return true;
	}

	/// <summary>
	/// Stage one kernel row directly on the HOST — the host's own fact, for a case whose subject is a
	/// report that must not be able to move or destroy that row. The world paths that put an item inside
	/// a world container are the game's own; the harness states the fact instead of replaying them.
	/// </summary>
	internal bool StageKernelRow(ulong actor, ulong instanceId, string definitionId, ItemLocation location, CharacterItemMsg item) =>
		Host.Services.GetRequiredService<ItemKernelAuthority>()
			.TrySpawn(actor, new ItemIdentity(instanceId, definitionId), location, item, out _, out _);

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

	private void Send(TestNode guest, NetMsg msg, object payload)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(Host.SteamId, msg, payload);
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

	/// <summary>The spawned item events a node has received so far (cumulative).</summary>
	internal int SpawnedEvents(TestNode node) => (node == G1 ? _g1Spawned : _g2Spawned).Count;

	/// <summary>The picked-up item events a node has received so far (cumulative).</summary>
	internal int PickedUpEvents(TestNode node) => (node == G1 ? _g1PickedUp : _g2PickedUp).Count;

	/// <summary>The dropped item events a node has received so far (cumulative).</summary>
	internal int DroppedEvents(TestNode node) => (node == G1 ? _g1Dropped : _g2Dropped).Count;

	/// <summary>The destroyed item events a node has received so far (cumulative).</summary>
	internal int DestroyedEvents(TestNode node) => (node == G1 ? _g1Destroyed : _g2Destroyed).Count;

	/// <summary>The carried-fact events a node has received so far (cumulative).</summary>
	internal List<(ulong Owner, CharacterItemMsg Item)> CarriedEvents(TestNode node) =>
		node == G1 ? [.. _g1Carried] : [.. _g2Carried];

	/// <summary>The id-watermark grants the node has received so far (cumulative — the rejoin grant is the assertion surface).</summary>
	internal List<ItemIdWatermarkMsg> Watermarks(TestNode node) =>
		[.. Received(node).Where(r => r.Msg == NetMsg.ItemIdWatermark).Select(r => NetPacket.DecodePayload<ItemIdWatermarkMsg>(r.Frame))];

	/// <summary>How many frames of one message type the node has received so far (cumulative).</summary>
	internal int ReceivedCount(TestNode node, NetMsg msg) => Received(node).Count(r => r.Msg == msg);

	/// <summary>
	/// How many kernel CommandRejected frames the node has received so far
	/// (cumulative). It is the surface for a refusal the item events deliberately do
	/// NOT report: a refused console write settles as its own family rather than
	/// through the pickup/block-drop rejection the guests' <c>ItemRejected</c> event
	/// carries.
	/// </summary>
	internal int CommandRejectedFrames(TestNode node) =>
		Received(node).Count(r => r.Msg == NetMsg.KernelEnvelope && IsCommandRejected(r.Frame));

	/// <summary>The verdicts those frames carry — the reason the host answered with, and the kind of report it judged.</summary>
	internal List<WireCommand> CommandRejections(TestNode node) =>
		[.. Received(node)
			.Where(r => r.Msg == NetMsg.KernelEnvelope && IsCommandRejected(r.Frame))
			.Select(r => NetPacket.DecodePayload<ProtocolFrame>(r.Frame).Command!.Command)];

	private static bool IsCommandRejected(byte[] frame)
	{
		var protocol = NetPacket.DecodePayload<ProtocolFrame>(frame);
		return protocol.Command?.Header?.PayloadType == WirePayloadType.CommandRejected;
	}

	/// <summary>How many frames of ANY message type the node has received so far
	/// (cumulative) — the SimTrace "Committed(n)" message-count surface.</summary>
	internal int ReceivedTotal(TestNode node) => Received(node).Count;

	/// <summary>
	/// Semantic diff between the legacy host terminal facts (world table +
	/// transfer table) and the production item kernel. Revision is not
	/// comparable yet because the legacy path has no aggregate revisions.
	/// </summary>
	internal ItemTerminalDiff CompareKernel() =>
		ItemDiagnosticsProjection.Compare(
			BuildLegacyActiveFacts(),
			ItemDiagnosticsProjection.BuildActiveFacts(Host.Services.GetRequiredService<ItemKernelAuthority>().QueryItems().Values),
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
				ItemLocation.NoLimb,
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
					ItemLocation.NoLimb,
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
