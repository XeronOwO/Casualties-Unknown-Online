using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The reliable channel can retransmit — a repeated ItemSpawn report (same
/// item id) must register and relay exactly once (ItemService idempotency).
/// Observed on a second guest: the star topology relays to every member except
/// the source, so a duplicate relay would show up there.
/// </summary>
public class ItemIdempotencyTests
{
	private const ulong HostId = 1001;
	private const ulong Guest1Id = 2001;
	private const ulong Guest2Id = 3001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void DuplicateItemSpawnReport_RelayedOnce()
	{
		var network = new FakeNetwork();
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var guest1Steam = new FakeSteamService(Guest1Id) { LobbyOwner = HostId, LobbyMembers = [HostId, Guest1Id, Guest2Id] };
		var guest2Steam = new FakeSteamService(Guest2Id) { LobbyOwner = HostId, LobbyMembers = [HostId, Guest1Id, Guest2Id] };
		// pumpFirstFrame: the mod discovery scan must run before any handshake
		// (a handshake arriving before it is refused as "mod check pending").
		var host = TestNode.Create(HostId, network, hostSteam, pumpFirstFrame: true);
		var guest1 = TestNode.Create(Guest1Id, network, guest1Steam, pumpFirstFrame: true);
		var guest2 = TestNode.Create(Guest2Id, network, guest2Steam, pumpFirstFrame: true);
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, Guest1Id, Guest2Id]; // both guests joined the lobby
		guest1.Steam.FireLobbyEntered(LobbyId);
		guest2.Steam.FireLobbyEntered(LobbyId);

		var relayed = 0;
		guest2.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.ItemSpawn)
			{
				relayed++;
			}
		};

		var sender = guest1.Services.GetRequiredService<PacketSender>();
		var report = new ItemSpawnMsg { ItemId = 42, Position = new NetVector2Msg { X = 1, Y = 2 } };
		sender.Send(HostId, NetMsg.ItemSpawn, report);
		Assert.Equal(1, relayed);

		sender.Send(HostId, NetMsg.ItemSpawn, report); // reliable retransmit duplicate
		Assert.Equal(1, relayed);
	}
}
