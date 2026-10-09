using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod native-API surface (Phase 4 Mod API remainder): the call is gated by
/// AccessNativeApi, the operation id's shape is checked before the adapter seam,
/// and each operation is reached through its own typed projection. There is no
/// untyped invoke path left to test — the projection's own signature is what
/// declares an operation's result type.
/// </summary>
[Trait("Category", "Integration")]
public class ModNativeApiTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestNativeApiMod NativeApiMod(TestNode node) =>
		(TestNativeApiMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestNativeApiMod);

	private static TestEchoMod EchoMod(TestNode node) =>
		(TestEchoMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestEchoMod);

	private static TestNode HostWith(FakeModNativeApiProvider fake)
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModNativeApiProvider>(fake)));
		return host;
	}

	[Fact]
	public void MissingAccessNativeApiPermission_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using var hostScope = host;

		var native = EchoMod(host).Context!.NativeApi;

		Assert.False(native.CanAccess, "AccessNativeApi is required: nothing is implicit.");
		Assert.False(native.CanInvoke(ModNativeApiOperations.LocalPlayerState));
		Assert.False(native.TryGetLocalPlayerState(out _));
	}

	[Fact]
	public void WithPermission_ReturnsTheProvidersTypedProjection()
	{
		var expected = new FakeNativeLocalPlayerState(10f, 20f, 90f, 80f, 70f, 60f, 50f, 37f, 45f, true, true);
		var fake = new FakeModNativeApiProvider { Result = expected };
		using var host = HostWith(fake);

		var native = NativeApiMod(host).Context!.NativeApi;

		Assert.True(native.CanAccess);
		Assert.True(native.CanInvoke(ModNativeApiOperations.LocalPlayerState));

		Assert.True(native.TryGetLocalPlayerState(out var state));
		Assert.Same(expected, state);
		Assert.Equal(1, fake.LocalPlayerStateCalls);

		Assert.Equal(10f, state.X);
		Assert.Equal(20f, state.Y);
		Assert.Equal(90f, state.BrainHealth);
		Assert.Equal(80f, state.Hunger);
		Assert.Equal(70f, state.Thirst);
		Assert.Equal(60f, state.Stamina);
		Assert.Equal(50f, state.Energy);
		Assert.Equal(37f, state.Temperature);
		Assert.Equal(45f, state.Consciousness);
		Assert.True(state.Alive);
		Assert.True(state.Conscious);
	}

	[Fact]
	public void NoLocalBodyToProject_IsRefused()
	{
		var fake = new FakeModNativeApiProvider { Result = null };
		using var host = HostWith(fake);

		var native = NativeApiMod(host).Context!.NativeApi;

		Assert.True(native.CanInvoke(ModNativeApiOperations.LocalPlayerState),
			"the operation is registered; what is missing is the body to project.");
		Assert.False(native.TryGetLocalPlayerState(out var state));
		Assert.Null(state);
		Assert.Equal(1, fake.LocalPlayerStateCalls);
	}

	[Fact]
	public void ProviderThatRefusesEveryOperation_IsRefused()
	{
		// The Runtime-only composition's shape (DisabledModNativeApiProvider): the adapter
		// still reaches the projection, which is what answers "unavailable".
		var fake = new FakeModNativeApiProvider { Available = false };
		using var host = HostWith(fake);

		var native = NativeApiMod(host).Context!.NativeApi;

		Assert.False(native.CanInvoke(ModNativeApiOperations.LocalPlayerState));
		Assert.False(native.TryGetLocalPlayerState(out var state));
		Assert.Null(state);
		Assert.Equal(1, fake.LocalPlayerStateCalls);
	}

	[Fact]
	public void UnregisteredOperation_IsNotInvokable()
	{
		var fake = new FakeModNativeApiProvider();
		using var host = HostWith(fake);

		var native = NativeApiMod(host).Context!.NativeApi;

		Assert.False(native.CanInvoke("unknown.operation"));
		Assert.Equal(1, fake.RegistrationProbes);
		Assert.Equal(0, fake.LocalPlayerStateCalls);
	}

	[Fact]
	public void MalformedOperationId_IsRefusedBeforeTheProviderSeesIt()
	{
		var fake = new FakeModNativeApiProvider();
		using var host = HostWith(fake);

		var native = NativeApiMod(host).Context!.NativeApi;

		Assert.False(native.CanInvoke(""));
		Assert.False(native.CanInvoke(new string('a', ModNativeApiPolicy.MaxOperationLength + 1)));
		Assert.False(native.CanInvoke("has space"));
		Assert.False(native.CanInvoke(ModNativeApiOperations.LocalPlayerState + "!"));
		Assert.Equal(0, fake.RegistrationProbes);
		Assert.Equal(0, fake.LocalPlayerStateCalls);
	}

	[Fact]
	public void PolicyRails_AreExact()
	{
		Assert.True(ModNativeApiPolicy.IsValidOperation("local.player.state"));
		Assert.True(ModNativeApiPolicy.IsValidOperation("a.b-c_d"));
		Assert.False(ModNativeApiPolicy.IsValidOperation(""));
		Assert.False(ModNativeApiPolicy.IsValidOperation("has space"));
		Assert.False(ModNativeApiPolicy.IsValidOperation(new string('a', ModNativeApiPolicy.MaxOperationLength + 1)));
	}
}
