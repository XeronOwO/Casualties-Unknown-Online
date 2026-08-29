using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Session;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

public class PlayerProjectionTests
{
	private const ulong HostId = 1001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HostPublishLocalState_CommitsPlayerKernelStatus()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var entities = host.Services.GetRequiredService<IEntitySyncControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		entities.PublishLocalState(
			new NetVector2(1, 2),
			new NetVector2(3, 4),
			new NetVector2(0, 0),
			isRight: true,
			standing: true,
			alive: false,
			conscious: false,
			crouching: false);

		var player = Assert.Single(authority.QueryPlayers()!.Players);
		Assert.Equal(HostId, player.SteamId);
		Assert.False(player.Alive);
		Assert.False(player.Conscious);
	}
}
