using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod multi-block structure placement surface: the call is gated by the
/// same SpawnEntity permission plus an active in-world session, malformed
/// requests are refused before the adapter seam, and the actual multi-cell
/// world writes are delegated to the Runtime → Game Adapter structure placement
/// boundary. The existing BlockPlaced channel is responsible for replication, so
/// this surface only tests the mod-side gate + delegation.
/// </summary>
public class ModStructurePlacementTests
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

		var placement = EchoMod(host).Context!.StructurePlacement;

		Assert.False(placement.CanPlace, "SpawnEntity is required for structure placement too.");
		Assert.False(placement.TryPlaceStructure("custom.shrine", 1, 2));
	}

	[Fact]
	public void WithPermission_ForwardsToGameAdapterStructurePlacer()
	{
		var fake = new FakeModStructurePlacer();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModStructurePlacer>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var placement = EntitySpawnMod(host).Context!.StructurePlacement;

		Assert.True(placement.CanPlace);
		Assert.True(placement.TryPlaceStructure("custom.shrine", 10, 20));

		var call = Assert.Single(fake.Calls);
		Assert.Equal("custom.shrine", call.StructureId);
		Assert.Equal(10, call.OriginX);
		Assert.Equal(20, call.OriginY);
	}

	[Fact]
	public void OutsideInWorldSession_IsRefusedBeforeAdapter()
	{
		var fake = new FakeModStructurePlacer();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModStructurePlacer>(fake)));

		using var hostScope = host;

		var placement = EntitySpawnMod(host).Context!.StructurePlacement;

		Assert.False(placement.TryPlaceStructure("custom.shrine", 1, 2));
		Assert.Empty(fake.Calls);
	}

	[Fact]
	public void InvalidRequest_IsRefusedBeforeAdapter()
	{
		var fake = new FakeModStructurePlacer();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModStructurePlacer>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var placement = EntitySpawnMod(host).Context!.StructurePlacement;

		Assert.False(placement.TryPlaceStructure("", 1, 2));
		Assert.False(placement.TryPlaceStructure("   ", 1, 2));
		Assert.False(placement.TryPlaceStructure(new string('a', ModEntitySpawnPolicy.MaxPrefabIdLength + 1), 1, 2));
		Assert.Empty(fake.Calls);
	}

	[Fact]
	public void AdapterFailure_IsReturnedAsFalse()
	{
		var fake = new FakeModStructurePlacer { Result = false };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModStructurePlacer>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var placement = EntitySpawnMod(host).Context!.StructurePlacement;

		Assert.False(placement.TryPlaceStructure("custom.shrine", 1, 2));
		Assert.Single(fake.Calls);
	}
}
