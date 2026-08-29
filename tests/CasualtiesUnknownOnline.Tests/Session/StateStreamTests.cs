using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The state stream's seq gate (PlayerStateHandler): the 20 Hz host→guest
/// stream is unreliable — stale snapshots (reordered) and duplicates must be
/// dropped, newer ones pass. Locked through the real handler over the fake
/// network.
/// </summary>
public class StateStreamTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void StaleAndDuplicateSequences_Dropped_NewerPass()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var entities = guest.Services.GetRequiredService<IEntitySyncControl>();
		var sender = host.Services.GetRequiredService<PacketSender>();

		sender.Send(GuestId, NetMsg.PlayerState, new PlayerStateMsg { Seq = 1 }, reliable: false);
		Assert.Equal(1u, entities.LastStateSeq);

		sender.Send(GuestId, NetMsg.PlayerState, new PlayerStateMsg { Seq = 1 }, reliable: false); // duplicate
		sender.Send(GuestId, NetMsg.PlayerState, new PlayerStateMsg { Seq = 0 }, reliable: false); // stale (reordered)
		Assert.Equal(1u, entities.LastStateSeq);

		sender.Send(GuestId, NetMsg.PlayerState, new PlayerStateMsg { Seq = 2 }, reliable: false);
		Assert.Equal(2u, entities.LastStateSeq);
	}

	[Fact]
	public void PlayerStateBatch_MissingPlayer_DoesNotRemoveExistingBuffer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var entities = guest.Services.GetRequiredService<IEntitySyncControl>();
		var sender = host.Services.GetRequiredService<PacketSender>();
		var hostEntityId = new NetworkEntityIdMsg(1, 1, 0);
		var guestEntityId = new NetworkEntityIdMsg(1, 2, 0);

		entities.ProcessPlayerJoin(new PlayerJoinMsg
		{
			HostSteamId = HostId,
			GuestSteamId = GuestId,
			HostEntityId = hostEntityId,
			GuestEntityId = guestEntityId,
			HostPosition = new NetVector2Msg(0f, 0f),
			GuestPosition = new NetVector2Msg(10f, 0f),
		});
		Assert.NotNull(entities.GetRemotePlayer(HostId));

		// An update-only stream batch that omits the host must not remove the
		// host buffer; lifecycle is owned by PlayerJoin/PlayerLeave.
		sender.Send(GuestId, NetMsg.PlayerState, new PlayerStateMsg
		{
			Seq = 1,
			Entities =
			[
				new EntityStateMsg
				{
					Id = guestEntityId,
					Position = new NetVector2Msg(5f, 5f),
				},
			],
		}, reliable: false);

		Assert.NotNull(entities.GetRemotePlayer(HostId));
		Assert.Equal(5f, entities.LocalPlayer.Position.X);
	}

	[Fact]
	public void PlayerLeave_RemovesRemoteBuffer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var entities = guest.Services.GetRequiredService<IEntitySyncControl>();
		var sender = host.Services.GetRequiredService<PacketSender>();
		var hostEntityId = new NetworkEntityIdMsg(1, 1, 0);
		var guestEntityId = new NetworkEntityIdMsg(1, 2, 0);

		entities.ProcessPlayerJoin(new PlayerJoinMsg
		{
			HostSteamId = HostId,
			GuestSteamId = GuestId,
			HostEntityId = hostEntityId,
			GuestEntityId = guestEntityId,
			HostPosition = new NetVector2Msg(0f, 0f),
			GuestPosition = new NetVector2Msg(10f, 0f),
		});
		Assert.NotNull(entities.GetRemotePlayer(HostId));

		sender.Send(GuestId, NetMsg.PlayerLeave, new PlayerLeaveMsg
		{
			SteamId = HostId,
			EntityId = hostEntityId,
		}, reliable: true);

		Assert.Null(entities.GetRemotePlayer(HostId));
	}
}
