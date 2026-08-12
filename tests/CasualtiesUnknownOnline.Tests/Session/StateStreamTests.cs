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
}
