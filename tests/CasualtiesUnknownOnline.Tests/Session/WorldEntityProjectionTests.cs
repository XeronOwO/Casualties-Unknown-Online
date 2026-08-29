using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Session;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

public class WorldEntityProjectionTests
{
	private const ulong HostId = 1001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HostReports_CommitKernelWorldEntities()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		world.ReportTrapConsumed(EntityEventKind.MineExploded, 1.2f, 2.8f, 5);
		world.ReportOpenedEntity(3.1f, 4.2f);
		world.ReportBuildingEntityHealth(5.1f, 6.2f, 7.5f);

		var state = authority.QueryWorldEntities();
		Assert.NotNull(state);
		var trap = Assert.Single(state!.Consumptions);
		Assert.Equal(1, trap.Position.X);
		Assert.Equal(2, trap.Position.Y);
		Assert.Equal((int)EntityEventKind.MineExploded, trap.Kind);
		Assert.Equal(5, trap.Extra);

		var opened = Assert.Single(state.OpenedEntities);
		Assert.Equal(new EntityPosition(3, 4), opened.Position);

		var health = Assert.Single(state.BuildingHealth);
		Assert.Equal(new EntityPosition(5, 6), health.Position);
		Assert.Equal(7.5f, health.Health);
	}
}
