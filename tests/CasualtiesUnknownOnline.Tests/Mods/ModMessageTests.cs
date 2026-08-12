using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod message channel over the real three-node star: a guest's report
/// reaches the host's copy of the mod (the sender rides through), the host's
/// broadcast reaches every member INCLUDING the host's own copy, unknown ids
/// and over-cap payloads are dropped, and every wrong-role / out-of-session
/// call is a no-op (a host "sending to host" talks to itself locally — it
/// must not loop back a frame to its own SteamId).
/// </summary>
public class ModMessageTests
{
	private const ulong HostId = 1001;
	private const ulong G1Id = 2001;
	private const ulong G2Id = 2002;
	private const ulong LobbyId = 9001;

	private sealed record World(TestNode Host, TestNode G1, TestNode G2);

	private static World CreateThreeNode()
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
		return new World(host, g1, g2);
	}

	private static TestEchoMod Echo(TestNode node) =>
		(TestEchoMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestEchoMod);

	[Fact]
	public void GuestReport_ReachesHostCopyWithSender()
	{
		var w = CreateThreeNode();
		var payload = Encoding.UTF8.GetBytes("hello from g1");

		Echo(w.G1).Context!.Network.SendToHost(payload);

		var received = Echo(w.Host).Received.Single();
		Assert.Equal(G1Id, received.Sender);
		Assert.Equal(payload, received.Payload);
		Assert.Empty(Echo(w.G2).Received); // no auto-relay — g2 sees nothing
	}

	[Fact]
	public void HostBroadcast_ReachesEveryMemberIncludingHost()
	{
		var w = CreateThreeNode();
		var payload = Encoding.UTF8.GetBytes("to everyone");

		Echo(w.Host).Context!.Network.Broadcast(payload);

		Assert.Equal([(HostId, payload)], Echo(w.Host).Received); // the local fire rides the same sender semantics
		Assert.Equal([(HostId, payload)], Echo(w.G1).Received);
		Assert.Equal([(HostId, payload)], Echo(w.G2).Received);
	}

	[Fact]
	public void HostDirectedSend_ReachesOnlyThatGuest()
	{
		var w = CreateThreeNode();

		Echo(w.Host).Context!.Network.SendToPeer(G2Id, [1, 2, 3]);

		Assert.Equal([(HostId, new byte[] { 1, 2, 3 })], Echo(w.G2).Received);
		Assert.Empty(Echo(w.G1).Received);
		Assert.Empty(Echo(w.Host).Received); // directed — no local fire
	}

	[Fact]
	public void UnknownModId_DroppedWithLog()
	{
		var w = CreateThreeNode();

		w.G1.Services.GetRequiredService<ModChannel>().SendToHost("test.missing", [9]);

		Assert.Empty(Echo(w.Host).Received);
	}

	[Fact]
	public void OverCapPayload_RefusedAtTheSender()
	{
		var w = CreateThreeNode();

		w.G1.Services.GetRequiredService<ModChannel>().SendToHost("test.echo", new byte[ModChannel.MaxPayloadBytes + 1]);

		Assert.Empty(Echo(w.Host).Received);
	}

	[Fact]
	public void ExactCapPayload_Passes()
	{
		var w = CreateThreeNode();

		w.G1.Services.GetRequiredService<ModChannel>().SendToHost("test.echo", new byte[ModChannel.MaxPayloadBytes]);

		Assert.Single(Echo(w.Host).Received);
	}

	[Fact]
	public void InactiveSession_SendToHostIsNoOp()
	{
		// No lobby — the node is outside any session.
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var steam = new FakeSteamService(G1Id);
		var g1 = TestNode.Create(G1Id, network, steam, clock, pumpFirstFrame: true);

		Echo(g1).Context!.Network.SendToHost([1]);

		Assert.Empty(Echo(g1).Received); // nothing sent, nothing routed back
	}

	[Fact]
	public void HostSendToHost_IsNoOp_NoSelfLoop()
	{
		var w = CreateThreeNode();
		var channel = w.Host.Services.GetRequiredService<ModChannel>();

		channel.SendToHost("test.echo", [1]); // wrong role — must not loop a frame back to the host's own SteamId

		Assert.Empty(Echo(w.Host).Received);
		Assert.Empty(Echo(w.G1).Received);
	}

	[Fact]
	public void GuestSendToPeer_IsNoOp()
	{
		var w = CreateThreeNode();
		var channel = w.G1.Services.GetRequiredService<ModChannel>();

		channel.SendToPeer("test.echo", G2Id, [1]); // wrong role — the star has no guest peer channels

		Assert.Empty(Echo(w.G2).Received);
	}

	[Fact]
	public void BroadcastWithNoMembers_StillFiresLocally()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true);
		host.Steam.FireLobbyCreated(LobbyId);

		Echo(host).Context!.Network.Broadcast([7]);

		Assert.Equal([(HostId, new byte[] { 7 })], Echo(host).Received);
	}
}
