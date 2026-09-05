using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// The three-node block-break replay world: a handshaken host + two guests on
/// the shared virtual clock, with the production-shaped host executor wired to
/// the real <see cref="BlockBreakArbitration"/> and <see cref="IItemControl"/>
/// surfaces. A break report with drops is first-writer-wins: the host accepts
/// it only when the sender's applied air-write was recorded, registers the
/// drops into the authoritative item table and relays the break (source
/// excluded); otherwise every drop gets an <c>ItemReject</c> back to the
/// breaker. The guests' received frames are the replay assertion surface.
/// </summary>
internal sealed class BlockBreakReplayWorld : IDisposable
{
	private const ulong HostId = 1001;
	private const ulong G1Id = 2001;
	private const ulong G2Id = 3001;
	private const ulong LobbyId = 9001;

	/// <summary>Mutable counter — a record's captured value would never advance.</summary>
	private sealed class Counter
	{
		internal int Value;
	}

	private readonly List<(NetMsg Msg, byte[] Frame)> _g1Received = [];
	private readonly List<(NetMsg Msg, byte[] Frame)> _g2Received = [];
	private readonly Dictionary<ulong, int> _acceptedBy = [];
	private readonly Counter _accepted = new();
	private readonly BlockBreakArbitration _arbitration = new();

	private BlockBreakReplayWorld(SimulationDriver driver, TestNode host, TestNode g1, TestNode g2)
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

	/// <summary>How many break reports the host accepted (first-writer-wins).</summary>
	internal int AcceptedCount => _accepted.Value;

	/// <summary>How many of a node's break reports the host accepted.</summary>
	internal int AcceptedBy(TestNode node) =>
		_acceptedBy.TryGetValue(node.SteamId, out var count) ? count : 0;

	/// <summary>How many BlockDamaged frames the node has received (cumulative).</summary>
	internal int BlockDamagedReceived(TestNode node) => Received(node).Count(r => r.Msg == NetMsg.BlockDamaged);

	/// <summary>How many KernelEnvelope command rejections the node has received (cumulative — the loser-rollback surface).</summary>
	internal int ItemRejectsReceived(TestNode node) => Received(node).Count(r => r.Msg == NetMsg.KernelEnvelope && IsCommandRejected(r.Frame));

	private static bool IsCommandRejected(byte[] frame)
	{
		var protocol = NetPacket.DecodePayload<ProtocolFrame>(frame);
		return protocol.Command?.Header?.PayloadType == WirePayloadType.CommandRejected;
	}

	/// <summary>Whether an accepted break's drop landed in the host's authoritative world-item table.</summary>
	internal bool IsDropRegistered(ulong itemId) => Host.Services.GetRequiredService<ItemService>().IsWorldItemRegistered(itemId);

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

	internal static BlockBreakReplayWorld Create()
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

		var world = new BlockBreakReplayWorld(driver, host, g1, g2);
		g1.Transport.MessageReceived += (_, frame) => world._g1Received.Add(((NetMsg)frame[0], frame));
		g2.Transport.MessageReceived += (_, frame) => world._g2Received.Add(((NetMsg)frame[0], frame));

		var worldControl = host.Services.GetRequiredService<IWorldControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		worldControl.BlockDamagedReceived += (sender, pos, damage, metalBonus, drops, buildingDrops) =>
		{
			// The production BlockBreakSync executor shape: only a BREAK (drops
			// attached) consults the one-shot record; a refused break rejects
			// every drop back to the breaker, never double-applies.
			if (drops is not { Count: > 0 } && buildingDrops is not { Count: > 0 })
			{
				return;
			}

			var cellX = (int)Math.Floor(pos.X);
			var cellY = (int)Math.Floor(pos.Y);
			if (!world._arbitration.TryAccept(sender, cellX, cellY))
			{
				if (drops is not null)
				{
					foreach (var drop in drops)
					{
						items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
					}
				}

				if (buildingDrops is not null)
				{
					foreach (var drop in buildingDrops)
					{
						items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
					}
				}

				return;
			}

			world._accepted.Value++;
			world._acceptedBy[sender] = world._acceptedBy.TryGetValue(sender, out var count) ? count + 1 : 1;
			items.FireBlockDropsReceived(sender, drops ?? []);
			items.FireBuildingDropsReceived(sender, buildingDrops ?? []);
			worldControl.BroadcastBlockDamaged(sender, pos, damage, metalBonus, drops, buildingDrops);
		};

		return world;
	}

	/// <summary>The host applied the sender's air-write (its BlockPlaced / SetBlock(0) report) — the first-writer record for the break arbitration.</summary>
	internal void AirWrite(TestNode node, int cellX, int cellY) =>
		_arbitration.RecordAppliedAirWrite(node.SteamId, cellX, cellY, now: Driver.NowMs / 1000f);

	/// <summary>One break report (break + drops = ONE message, one verdict) from a guest.</summary>
	internal void Break(TestNode guest, int cellX, int cellY, IReadOnlyList<BlockDropEntryMsg> drops, bool metalBonus)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(HostId, NetMsg.BlockDamaged, new BlockDamagedMsg
		{
			Position = new NetVector2Msg(cellX + 0.5f, cellY + 0.5f),
			Damage = 100f,
			MetalBonus = metalBonus,
			Drops = [.. drops],
		});
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
