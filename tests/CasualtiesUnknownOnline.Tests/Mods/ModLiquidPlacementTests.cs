using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod liquid-tile placement surface: the call is gated by the same
/// SpawnEntity permission plus an active in-world session, malformed requests
/// are refused before the adapter seam, and the actual vanilla fluid writes are
/// delegated to the Runtime → Game Adapter liquid-placement boundary. The
/// existing host fluid stream is responsible for replication, so this surface
/// only tests the mod-side gate + delegation.
/// </summary>
public class ModLiquidPlacementTests
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

		var placement = EchoMod(host).Context!.LiquidPlacement;

		Assert.False(placement.CanPlace, "SpawnEntity is required for liquid placement too.");
		Assert.False(placement.TryPlaceLiquid("custom.liquid", 1, 2));
		Assert.False(placement.TryFloodFill("custom.liquid", 1, 2, 32));
	}

	[Fact]
	public void WithPermission_ForwardsPlaceToGameAdapterLiquidPlacer()
	{
		var fake = new FakeModLiquidPlacer();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModLiquidPlacer>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var placement = EntitySpawnMod(host).Context!.LiquidPlacement;

		Assert.True(placement.CanPlace);
		Assert.True(placement.TryPlaceLiquid("custom.liquid", 10, 20));

		var call = Assert.Single(fake.PlaceCalls);
		Assert.Equal("custom.liquid", call.LiquidTileId);
		Assert.Equal(10, call.X);
		Assert.Equal(20, call.Y);
	}

	[Fact]
	public void WithPermission_ForwardsFloodFillToGameAdapterLiquidPlacer()
	{
		var fake = new FakeModLiquidPlacer();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModLiquidPlacer>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var placement = EntitySpawnMod(host).Context!.LiquidPlacement;

		Assert.True(placement.CanPlace);
		Assert.True(placement.TryFloodFill("custom.liquid", 11, 21, 64));

		var call = Assert.Single(fake.FloodFillCalls);
		Assert.Equal("custom.liquid", call.LiquidTileId);
		Assert.Equal(11, call.StartX);
		Assert.Equal(21, call.StartY);
		Assert.Equal(64, call.MaxFill);
	}

	[Fact]
	public void OutsideInWorldSession_IsRefusedBeforeAdapter()
	{
		var fake = new FakeModLiquidPlacer();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModLiquidPlacer>(fake)));

		using var hostScope = host;

		var placement = EntitySpawnMod(host).Context!.LiquidPlacement;

		Assert.False(placement.TryPlaceLiquid("custom.liquid", 1, 2));
		Assert.False(placement.TryFloodFill("custom.liquid", 1, 2, 32));
		Assert.Empty(fake.PlaceCalls);
		Assert.Empty(fake.FloodFillCalls);
	}

	[Fact]
	public void InvalidRequest_IsRefusedBeforeAdapter()
	{
		var fake = new FakeModLiquidPlacer();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModLiquidPlacer>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var placement = EntitySpawnMod(host).Context!.LiquidPlacement;

		Assert.False(placement.TryPlaceLiquid("", 1, 2));
		Assert.False(placement.TryPlaceLiquid("   ", 1, 2));
		Assert.False(placement.TryPlaceLiquid(new string('a', ModEntitySpawnPolicy.MaxPrefabIdLength + 1), 1, 2));
		Assert.False(placement.TryFloodFill("", 1, 2, 32));
		Assert.False(placement.TryFloodFill("   ", 1, 2, 32));
		Assert.False(placement.TryFloodFill(new string('a', ModEntitySpawnPolicy.MaxPrefabIdLength + 1), 1, 2, 32));
		Assert.Empty(fake.PlaceCalls);
		Assert.Empty(fake.FloodFillCalls);
	}

	[Fact]
	public void AdapterFailure_IsReturnedAsFalse()
	{
		var fake = new FakeModLiquidPlacer { Result = false };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModLiquidPlacer>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var placement = EntitySpawnMod(host).Context!.LiquidPlacement;

		Assert.False(placement.TryPlaceLiquid("custom.liquid", 1, 2));
		Assert.Single(fake.PlaceCalls);
		Assert.False(placement.TryFloodFill("custom.liquid", 1, 2, 32));
		Assert.Single(fake.FloodFillCalls);
	}
}
