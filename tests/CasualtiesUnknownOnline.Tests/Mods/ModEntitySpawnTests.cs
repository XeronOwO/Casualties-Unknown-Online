using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod entity-spawn surface (Phase 4 Mod API remainder): the call is
/// gated by SpawnEntity and an active in-world session, malformed requests
/// are refused before the adapter seam, and the actual creation is delegated
/// to the Runtime → Game Adapter boundary. The existing runtime-entity channel
/// is responsible for replication, so this surface only tests the mod-side
/// gate + delegation.
/// </summary>
public class ModEntitySpawnTests
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

		var spawn = EchoMod(host).Context!.EntitySpawn;

		Assert.False(spawn.CanSpawn, "SpawnEntity is required: nothing is implicit.");
		Assert.False(spawn.TrySpawn("landmine", 1f, 2f, 0f));
	}

	[Fact]
	public void WithPermission_ForwardsToGameAdapterSpawner()
	{
		var fake = new FakeModEntitySpawner();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModEntitySpawner>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var spawn = EntitySpawnMod(host).Context!.EntitySpawn;

		Assert.True(spawn.CanSpawn);
		Assert.True(spawn.TrySpawn("landmine", 10f, 20f, 45f));

		var call = Assert.Single(fake.Calls);
		Assert.Equal("landmine", call.PrefabId);
		Assert.Equal(10f, call.X);
		Assert.Equal(20f, call.Y);
		Assert.Equal(45f, call.Rotation);
	}

	[Fact]
	public void OutsideInWorldSession_IsRefusedBeforeAdapter()
	{
		var fake = new FakeModEntitySpawner();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModEntitySpawner>(fake)));

		using var hostScope = host;

		var spawn = EntitySpawnMod(host).Context!.EntitySpawn;

		Assert.False(spawn.TrySpawn("landmine", 1f, 2f, 0f));
		Assert.Empty(fake.Calls);
	}

	[Fact]
	public void InvalidRequest_IsRefusedBeforeAdapter()
	{
		var fake = new FakeModEntitySpawner();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModEntitySpawner>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var spawn = EntitySpawnMod(host).Context!.EntitySpawn;

		Assert.False(spawn.TrySpawn("", 1f, 2f, 0f));
		Assert.False(spawn.TrySpawn("landmine", float.NaN, 2f, 0f));
		Assert.False(spawn.TrySpawn("landmine", 1f, 2f, float.PositiveInfinity));
		Assert.Empty(fake.Calls);
	}

	[Fact]
	public void AdapterFailure_IsReturnedAsFalse()
	{
		var fake = new FakeModEntitySpawner { Result = false };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModEntitySpawner>(fake)));

		using var hostScope = host;
		host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

		var spawn = EntitySpawnMod(host).Context!.EntitySpawn;

		Assert.False(spawn.TrySpawn("landmine", 1f, 2f, 0f));
		Assert.Single(fake.Calls);
	}

	[Fact]
	public void PolicyRails_AreExactAndNoSilentFallback()
	{
		Assert.True(ModEntitySpawnPolicy.IsValidPrefabId("landmine"));
		Assert.True(ModEntitySpawnPolicy.IsValidPrefabId("Special/CrystalDistort"));
		Assert.False(ModEntitySpawnPolicy.IsValidPrefabId(""));
		Assert.False(ModEntitySpawnPolicy.IsValidPrefabId("   "));
		Assert.False(ModEntitySpawnPolicy.IsValidPrefabId(new string('a', ModEntitySpawnPolicy.MaxPrefabIdLength + 1)));

		Assert.True(ModEntitySpawnPolicy.IsValidPosition(1f, 2f));
		Assert.False(ModEntitySpawnPolicy.IsValidPosition(float.NaN, 0f));
		Assert.False(ModEntitySpawnPolicy.IsValidPosition(0f, float.NegativeInfinity));
		Assert.True(ModEntitySpawnPolicy.IsValidRotation(0f));
		Assert.False(ModEntitySpawnPolicy.IsValidRotation(float.NaN));
	}
}
