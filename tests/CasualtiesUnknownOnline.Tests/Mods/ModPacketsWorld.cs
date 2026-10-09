using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The declared-packet cases' shared world: three REAL nodes (a host and two
/// guests over the production composition root, transport and Steam replaced by
/// fakes) plus the lookups, the value helpers and the frame helper every case
/// needs. Internal rather than public: the packet cases are split by behaviour
/// family across three test classes, and this is the one piece they share.
/// </summary>
internal sealed record ModPacketsWorld(TestNode Host, TestNode G1, TestNode G2)
{
	internal const ulong HostId = 1001;
	internal const ulong G1Id = 2001;
	internal const ulong G2Id = 2002;
	internal const ulong LobbyId = 9001;

	/// <summary>The fixture's two-step and three-step chains, as "stage:marker" rows.</summary>
	internal static readonly string[] TwoStep = ["Validate:validate", "Apply:apply"];

	internal static readonly string[] ThreeStep = ["Validate:validate", "Apply:apply", "Observe:observe"];

	internal static ModPacketsWorld CreateThreeNode()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var g1Steam = new FakeSteamService(G1Id) { LobbyOwner = HostId, LobbyMembers = [HostId, G1Id, G2Id] };
		var g2Steam = new FakeSteamService(G2Id) { LobbyOwner = HostId, LobbyMembers = [HostId, G1Id, G2Id] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true);
		var g1 = TestNode.Create(G1Id, network, g1Steam, clock, pumpFirstFrame: true);
		var g2 = TestNode.Create(G2Id, network, g2Steam, clock, pumpFirstFrame: true);
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, G1Id, G2Id];
		g1.Steam.FireLobbyEntered(LobbyId);
		g2.Steam.FireLobbyEntered(LobbyId);
		return new ModPacketsWorld(host, g1, g2);
	}

	internal static TestPacketMod Fixture(TestNode node) =>
		(TestPacketMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestPacketMod);

	internal static IModPackets Surface(TestNode node) => Fixture(node).Context!.Packets;

	/// <summary>The value each run of one packet read on this copy, in run order.</summary>
	internal static List<ModValue> ValuesOf(TestNode node, string packetId) => Fixture(node).ValuesOf(packetId);

	/// <summary>The ordinary value the fixture's packets carry.</summary>
	internal static ModValue Step(int step) => ModValue.Integer(step);

	/// <summary>A value that is structurally legal and still cannot fit the payload rail.</summary>
	internal static ModValue OverCapValue() => ModValues.OverCap();

	/// <summary>A declared-packet frame as a peer would put it on the wire — the value encoded the way the channel carries it.</summary>
	internal static byte[] Frame(string packetId, ModValue value)
	{
		if (!ModValueCodec.TryEncode(value, ModChannel.MaxPayloadBytes, out var encoded, out var refusal))
		{
			throw new InvalidOperationException($"the fixture's frame value cannot be encoded: {refusal}");
		}

		return NetPacket.Encode(NetMsg.ModMessage, new ModMessageMsg
		{
			ModId = "test.packets",
			PacketId = packetId,
			Payload = encoded,
		});
	}
}
