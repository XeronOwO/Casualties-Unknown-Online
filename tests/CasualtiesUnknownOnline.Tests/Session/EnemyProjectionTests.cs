using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Session;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

public class EnemyProjectionTests
{
	private const ulong HostId = 1001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HostPublishEnemyStates_CommitsKernelEnemyTable()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var enemies = host.Services.GetRequiredService<EnemySyncService>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var entity = new EnemyEntity(new NetworkEntityId(1, 2, 0))
		{
			PrefabId = "spider",
			Health = 12f,
			RuntimeSpawned = true,
			Stunned = true,
		};

		enemies.PublishEnemyStates([entity]);

		var enemy = Assert.Single(authority.QueryEnemies()!.Enemies);
		Assert.Equal(new EntityId(1, 2, 0), enemy.EntityId);
		Assert.Equal("spider", enemy.PrefabId);
		Assert.Equal(12f, enemy.Health);
		Assert.True(enemy.RuntimeSpawned);
		Assert.True(enemy.Stunned);
	}
}
