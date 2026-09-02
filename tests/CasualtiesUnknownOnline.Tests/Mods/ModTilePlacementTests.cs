using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod tile/block placement surface: the call is gated by the same
/// SpawnEntity permission plus an active in-world session, malformed requests
/// are refused before the adapter seam, and the actual vanilla block write is
/// delegated to the Runtime → Game Adapter tile-placement boundary. The
/// existing BlockPlaced channel is responsible for replication, so this surface
/// only tests the mod-side gate + delegation.
/// </summary>
public class ModTilePlacementTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestEntitySpawnMod EntitySpawnMod(TestNode node) =>
		(TestEntitySpawnMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestEntitySpawnMod);

	private static TestEchoMod EchoMod(TestNode node) =>
		(TestEchoMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestEchoMod);

	[Fact]
	public void MissingSpawnEntityPermission_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using var hostScope = host;

		var placement = EchoMod(host).Context!.TilePlacement;

		Assert.False(placement.CanPlace, "SpawnEntity is required for tile placement too.");
		Assert.False(placement.TryPlaceBlock("custom.tile", 1, 2));
	}

	[Fact]
	public void WithPermission_ForwardsToGameAdapterTilePlacer()
	{
		var fake = new FakeModTilePlacer();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModTilePlacer>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var placement = EntitySpawnMod(host).Context!.TilePlacement;

		Assert.True(placement.CanPlace);
		Assert.True(placement.TryPlaceBlock("custom.tile", 10, 20));

		var call = Assert.Single(fake.Calls);
		Assert.Equal("custom.tile", call.TileId);
		Assert.Equal(10, call.X);
		Assert.Equal(20, call.Y);
	}

	[Fact]
	public void OutsideInWorldSession_IsRefusedBeforeAdapter()
	{
		var fake = new FakeModTilePlacer();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModTilePlacer>(fake)));

		using var hostScope = host;

		var placement = EntitySpawnMod(host).Context!.TilePlacement;

		Assert.False(placement.TryPlaceBlock("custom.tile", 1, 2));
		Assert.Empty(fake.Calls);
	}

	[Fact]
	public void InvalidRequest_IsRefusedBeforeAdapter()
	{
		var fake = new FakeModTilePlacer();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModTilePlacer>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var placement = EntitySpawnMod(host).Context!.TilePlacement;

		Assert.False(placement.TryPlaceBlock("", 1, 2));
		Assert.False(placement.TryPlaceBlock("   ", 1, 2));
		Assert.False(placement.TryPlaceBlock(new string('a', ModEntitySpawnPolicy.MaxPrefabIdLength + 1), 1, 2));
		Assert.Empty(fake.Calls);
	}

	[Fact]
	public void AdapterFailure_IsReturnedAsFalse()
	{
		var fake = new FakeModTilePlacer { Result = false };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModTilePlacer>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var placement = EntitySpawnMod(host).Context!.TilePlacement;

		Assert.False(placement.TryPlaceBlock("custom.tile", 1, 2));
		Assert.Single(fake.Calls);
	}
}
