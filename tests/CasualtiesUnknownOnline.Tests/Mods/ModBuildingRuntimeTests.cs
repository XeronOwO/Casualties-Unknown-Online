using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The local runtime building hook surface. It is the CUO-safe replacement for
/// CUCoreLib's building <c>ConfigurePrefab</c> / <c>ConfigureInstance</c>
/// callbacks: a mod registers a hook per building id, receives a plain request,
/// and returns component type names for the Game Adapter to attach. The hook
/// table is per-mod, local-only, and does not require a static content binding.
/// </summary>
public class ModBuildingRuntimeTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static IModBuildingRuntime BuildingsOf(TestNode node) =>
		((TestDataMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestDataMod)).Context!.BuildingRuntime;

	[Fact]
	public void Register_Has_Unregister_HappyPath()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var buildings = BuildingsOf(host);

		Assert.True(buildings.TryRegisterPrefabHook("building.crate", _ => ["MyMod.CrateBehaviour, MyMod"]));
		Assert.True(buildings.HasPrefabHook("building.crate"));
		Assert.Single(buildings.PrefabHookBuildingIds);
		Assert.Equal(1, buildings.PrefabHookCount);

		Assert.True(buildings.TryRegisterInstanceHook("building.crate", _ => ["MyMod.CrateInstanceEffect, MyMod"]));
		Assert.True(buildings.HasInstanceHook("building.crate"));
		Assert.Single(buildings.InstanceHookBuildingIds);
		Assert.Equal(1, buildings.InstanceHookCount);

		Assert.True(buildings.TryUnregisterPrefabHook("building.crate"));
		Assert.False(buildings.HasPrefabHook("building.crate"));
		Assert.Empty(buildings.PrefabHookBuildingIds);
		Assert.Equal(0, buildings.PrefabHookCount);
		Assert.False(buildings.TryUnregisterPrefabHook("building.crate"));

		Assert.True(buildings.TryUnregisterInstanceHook("building.crate"));
		Assert.False(buildings.HasInstanceHook("building.crate"));
		Assert.Empty(buildings.InstanceHookBuildingIds);
		Assert.Equal(0, buildings.InstanceHookCount);
		Assert.False(buildings.TryUnregisterInstanceHook("building.crate"));
	}

	[Fact]
	public void Register_RejectsNullInvalidAndDuplicate()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var buildings = BuildingsOf(host);

		Assert.False(buildings.TryRegisterPrefabHook("", _ => ["x"]));
		Assert.False(buildings.TryRegisterPrefabHook("building.crate", null!));
		Assert.False(buildings.TryRegisterInstanceHook("", _ => ["x"]));
		Assert.False(buildings.TryRegisterInstanceHook("building.crate", null!));

		Assert.True(buildings.TryRegisterPrefabHook("building.crate", _ => ["x"]));
		Assert.False(buildings.TryRegisterPrefabHook("building.crate", _ => ["y"]));
		Assert.True(buildings.TryRegisterInstanceHook("building.crate", _ => ["x"]));
		Assert.False(buildings.TryRegisterInstanceHook("building.crate", _ => ["y"]));
	}

	[Fact]
	public void Hooks_DoNotRequireBuildingContentBinding()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var buildings = BuildingsOf(host);

		Assert.True(buildings.TryRegisterPrefabHook("building.not-bound-yet", _ => ["x"]));
		Assert.True(buildings.HasPrefabHook("building.not-bound-yet"));
	}

	[Fact]
	public void Store_ReturnsRegisteredDelegatesAndScopesByModImplicitly()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var buildings = BuildingsOf(host);
		var store = host.Services.GetRequiredService<ModService>().BuildingRuntimeStore;

		Assert.True(buildings.TryRegisterPrefabHook("building.limb",
			request => request.BuildingId == "building.limb" ? ["effect"] : null));
		Assert.True(store.TryGetPrefabHook("test.data", "building.limb", out var prefabHook));
		Assert.NotNull(prefabHook);
		Assert.Equal(["effect"], prefabHook!(new ModBuildingPrefabRequest { BuildingId = "building.limb" }));
		Assert.False(store.TryGetPrefabHook("test.data", "building.missing", out _));
		Assert.False(store.TryGetPrefabHook("other.mod", "building.limb", out _));

		Assert.True(buildings.TryRegisterInstanceHook("building.limb",
			request => request.TemplateId == "crate" ? ["instance-effect"] : null));
		Assert.True(store.TryGetInstanceHook("test.data", "building.limb", out var instanceHook));
		Assert.NotNull(instanceHook);
		Assert.Equal(["instance-effect"], instanceHook!(new ModBuildingInstanceRequest
		{
			BuildingId = "building.limb",
			TemplateId = "crate"
		}));
		Assert.False(store.TryGetInstanceHook("test.data", "building.missing", out _));
		Assert.False(store.TryGetInstanceHook("other.mod", "building.limb", out _));
	}
}
