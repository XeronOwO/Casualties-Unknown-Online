using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The local moodle-presentation resolver surface. It is the CUO-safe
/// replacement for CUCoreLib's body/limb moodle callbacks: a mod registers a
/// resolver per runtime status id, receives a plain
/// <see cref="ModStatusMoodleRequest"/> (opaque payload + stable limb identity),
/// and returns a static moodle id. The resolver table is per-mod, local-only,
/// and does not require a static status content binding.
/// </summary>
public class ModStatusMoodleRuntimeTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static IModMoodleRuntime MoodlesOf(TestNode node) =>
		((TestDataMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestDataMod)).Context!.MoodleRuntime;

	private static IModStatusRuntime StatusOf(TestNode node) =>
		((TestDataMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestDataMod)).Context!.StatusRuntime;

	[Fact]
	public void Register_Has_Unregister_HappyPath()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var moodles = MoodlesOf(host);

		Assert.True(moodles.TryRegisterResolver("status.poison", _ => "moodle.poison"));
		Assert.True(moodles.HasResolver("status.poison"));
		Assert.Single(moodles.ResolverStatusIds);
		Assert.Equal(1, moodles.ResolverCount);

		Assert.True(moodles.TryUnregisterResolver("status.poison"));
		Assert.False(moodles.HasResolver("status.poison"));
		Assert.Empty(moodles.ResolverStatusIds);
		Assert.Equal(0, moodles.ResolverCount);
		Assert.False(moodles.TryUnregisterResolver("status.poison"));
	}

	[Fact]
	public void Register_RejectsNullInvalidAndDuplicate()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var moodles = MoodlesOf(host);

		Assert.False(moodles.TryRegisterResolver("", _ => "moodle.poison"));
		Assert.False(moodles.TryRegisterResolver("status.poison", null!));
		Assert.True(moodles.TryRegisterResolver("status.poison", _ => "moodle.poison"));
		Assert.False(moodles.TryRegisterResolver("status.poison", _ => "moodle.other"));
	}

	[Fact]
	public void Resolver_DoesNotRequireRuntimeStatusDeclaration()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var moodles = MoodlesOf(host);
		var status = StatusOf(host);

		Assert.True(moodles.TryRegisterResolver("status.not-declared-yet", _ => "moodle.pending"));
		Assert.True(status.TryDeclare("status.not-declared-yet", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.True(moodles.HasResolver("status.not-declared-yet"));
	}

	[Fact]
	public void Resolver_StoreReturnsRegisteredDelegateAndScopesByModImplicitly()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var moodles = MoodlesOf(host);
		var store = host.Services.GetRequiredService<ModService>().StatusStore;

		Assert.True(moodles.TryRegisterResolver("status.limb", _ => "moodle.limb"));
		Assert.True(store.TryGetMoodleResolver("test.data", "status.limb", out var resolver));
		Assert.NotNull(resolver);
		Assert.Equal("moodle.limb", resolver!(new ModStatusMoodleRequest { StatusId = "status.limb" }));
		Assert.False(store.TryGetMoodleResolver("test.data", "status.missing", out _));
		Assert.False(store.TryGetMoodleResolver("other.mod", "status.limb", out _));
	}
}
