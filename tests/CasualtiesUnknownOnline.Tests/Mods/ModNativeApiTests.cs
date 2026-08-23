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
/// AccessNativeApi, malformed operations and unsafe values are refused before
/// the adapter seam, unsafe provider results are refused after it, and the
/// read-only local-player projection works through the typed convenience.
/// </summary>
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

	[Fact]
	public void MissingAccessNativeApiPermission_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using var hostScope = host;

		var native = EchoMod(host).Context!.NativeApi;

		Assert.False(native.CanAccess, "AccessNativeApi is required: nothing is implicit.");
		Assert.False(native.CanInvoke(ModNativeApiOperations.LocalPlayerState));
		Assert.False(native.TryInvoke(ModNativeApiOperations.LocalPlayerState, [], out _));
		Assert.False(native.TryGetLocalPlayerState(out _));
	}

	[Fact]
	public void WithPermission_ForwardsToProviderAndReturnsSafeResult()
	{
		var expected = new FakeNativeLocalPlayerState(10f, 20f, 90f, 80f, 70f, 60f, 50f, 37f, 45f, true, true);
		var fake = new FakeModNativeApiProvider { Result = expected };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModNativeApiProvider>(fake)));

		using var hostScope = host;

		var native = NativeApiMod(host).Context!.NativeApi;

		Assert.True(native.CanAccess);
		Assert.True(native.CanInvoke(ModNativeApiOperations.LocalPlayerState));
		Assert.True(native.TryInvoke(ModNativeApiOperations.LocalPlayerState, [], out var result));
		Assert.Same(expected, result);

		var call = Assert.Single(fake.Calls);
		Assert.Equal(ModNativeApiOperations.LocalPlayerState, call.Operation);
		Assert.Empty(call.Arguments);

		Assert.True(native.TryGetLocalPlayerState(out var state));
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
	public void UnknownOperation_IsRefused()
	{
		var fake = new FakeModNativeApiProvider();
		fake.RegisteredOperations.Clear();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModNativeApiProvider>(fake)));

		using var hostScope = host;

		var native = NativeApiMod(host).Context!.NativeApi;

		Assert.False(native.CanInvoke("unknown.operation"));
		Assert.False(native.TryInvoke("unknown.operation", [], out _));
		Assert.Single(fake.Calls);
		Assert.Equal("unknown.operation", fake.Calls[0].Operation);
	}

	[Fact]
	public void MalformedOperationOrUnsafeArguments_IsRefusedBeforeProvider()
	{
		var fake = new FakeModNativeApiProvider();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModNativeApiProvider>(fake)));

		using var hostScope = host;

		var native = NativeApiMod(host).Context!.NativeApi;
		var tooLong = new string('a', ModNativeApiPolicy.MaxOperationLength + 1);

		Assert.False(native.TryInvoke("", [], out _));
		Assert.False(native.TryInvoke(tooLong, [], out _));
		Assert.False(native.TryInvoke(ModNativeApiOperations.LocalPlayerState, [new object()], out _));
		Assert.Empty(fake.Calls);
	}

	[Fact]
	public void UnsafeProviderResult_IsRefusedAfterSeam()
	{
		var fake = new FakeModNativeApiProvider { Result = new object() };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModNativeApiProvider>(fake)));

		using var hostScope = host;

		var native = NativeApiMod(host).Context!.NativeApi;

		Assert.False(native.TryInvoke(ModNativeApiOperations.LocalPlayerState, [], out var result));
		Assert.Null(result);
		Assert.Single(fake.Calls);
	}

	[Fact]
	public void ArgumentCountCap_IsRefusedBeforeProvider()
	{
		var fake = new FakeModNativeApiProvider();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(ServiceDescriptor.Singleton<IModNativeApiProvider>(fake)));

		using var hostScope = host;

		var native = NativeApiMod(host).Context!.NativeApi;
		var arguments = Enumerable.Repeat<object?>(1, ModNativeApiPolicy.MaxArguments + 1).ToArray();

		Assert.False(native.TryInvoke(ModNativeApiOperations.LocalPlayerState, arguments, out _));
		Assert.Empty(fake.Calls);
	}

	[Fact]
	public void PolicyRails_AreExact()
	{
		Assert.True(ModNativeApiPolicy.IsValidOperation("local.player.state"));
		Assert.True(ModNativeApiPolicy.IsValidOperation("a.b-c_d"));
		Assert.False(ModNativeApiPolicy.IsValidOperation(""));
		Assert.False(ModNativeApiPolicy.IsValidOperation("has space"));
		Assert.False(ModNativeApiPolicy.IsValidOperation(new string('a', ModNativeApiPolicy.MaxOperationLength + 1)));

		Assert.True(ModNativeApiPolicy.IsValidArguments([]));
		Assert.True(ModNativeApiPolicy.IsValidArguments([1, "x", true, 1.5f, new byte[] { 1, 2 }]));
		Assert.False(ModNativeApiPolicy.IsValidArguments(new object?[ModNativeApiPolicy.MaxArguments + 1]));
		Assert.False(ModNativeApiPolicy.IsValidArguments([new object()]));

		Assert.True(ModNativeApiPolicy.IsSafeResult(null));
		Assert.True(ModNativeApiPolicy.IsSafeResult("ok"));
		Assert.True(ModNativeApiPolicy.IsSafeResult(new float[] { 1f, 2f }));
		Assert.True(ModNativeApiPolicy.IsSafeResult(new FakeNativeLocalPlayerState(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false, false)));
		Assert.False(ModNativeApiPolicy.IsSafeResult(new object()));
	}
}
