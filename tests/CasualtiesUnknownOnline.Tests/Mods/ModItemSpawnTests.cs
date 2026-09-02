using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod item-spawn surface: the call is gated by the same SpawnEntity
/// permission plus an active in-world session, malformed requests are refused
/// before the adapter seam, and the actual creation is delegated to the
/// Runtime → Game Adapter item-spawn boundary. The existing item-domain
/// channel is responsible for replication, so this surface only tests the
/// mod-side gate + delegation.
/// </summary>
public class ModItemSpawnTests
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

		var spawn = EchoMod(host).Context!.ItemSpawn;

		Assert.False(spawn.CanSpawn, "SpawnEntity is required for item spawns too.");
		Assert.False(spawn.TrySpawn("wooden.sword", 1f, 2f, 0f));
	}

	[Fact]
	public void WithPermission_ForwardsToGameAdapterItemSpawner()
	{
		var fake = new FakeModItemSpawner();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModItemSpawner>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var spawn = EntitySpawnMod(host).Context!.ItemSpawn;

		Assert.True(spawn.CanSpawn);
		Assert.True(spawn.TrySpawn("wooden.sword", 10f, 20f, 45f));

		var call = Assert.Single(fake.Calls);
		Assert.Equal("wooden.sword", call.ItemId);
		Assert.Equal(10f, call.X);
		Assert.Equal(20f, call.Y);
		Assert.Equal(45f, call.Rotation);
	}

	[Fact]
	public void OutsideInWorldSession_IsRefusedBeforeAdapter()
	{
		var fake = new FakeModItemSpawner();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModItemSpawner>(fake)));

		using var hostScope = host;

		var spawn = EntitySpawnMod(host).Context!.ItemSpawn;

		Assert.False(spawn.TrySpawn("wooden.sword", 1f, 2f, 0f));
		Assert.Empty(fake.Calls);
	}

	[Fact]
	public void InvalidRequest_IsRefusedBeforeAdapter()
	{
		var fake = new FakeModItemSpawner();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModItemSpawner>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var spawn = EntitySpawnMod(host).Context!.ItemSpawn;

		Assert.False(spawn.TrySpawn("", 1f, 2f, 0f));
		Assert.False(spawn.TrySpawn("wooden.sword", float.NaN, 2f, 0f));
		Assert.False(spawn.TrySpawn("wooden.sword", 1f, 2f, float.PositiveInfinity));
		Assert.Empty(fake.Calls);
	}

	[Fact]
	public void AdapterFailure_IsReturnedAsFalse()
	{
		var fake = new FakeModItemSpawner { Result = false };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModItemSpawner>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var spawn = EntitySpawnMod(host).Context!.ItemSpawn;

		Assert.False(spawn.TrySpawn("wooden.sword", 1f, 2f, 0f));
		Assert.Single(fake.Calls);
	}
}
