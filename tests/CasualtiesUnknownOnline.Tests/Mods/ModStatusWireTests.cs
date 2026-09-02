using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed status transport seam (phase 2): shared body/limb statuses are
/// published by the host as <see cref="ModStatusUpdate"/> frames over the
/// existing <see cref="IModNetwork"/> channel and applied/removed on the guest
/// mirror. Local-only and host-authoritative scopes do not use this seam, and
/// non-status payloads are not consumed.
/// </summary>
public class ModStatusWireTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestStatusSyncMod SyncMod(TestNode node) =>
		(TestStatusSyncMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestStatusSyncMod);

	private static IModStatusRuntime StatusOf(TestNode node) => SyncMod(node).Context!.StatusRuntime;

	private static IModStatusTransport TransportOf(TestNode node) => SyncMod(node).Context!.StatusTransport;

	[Fact]
	public void SharedBodyStatus_HostBroadcast_WritesAuthorityAndAppliesGuestMirror()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostStatus = StatusOf(host);
		var guestStatus = StatusOf(guest);

		Assert.True(hostStatus.TryDeclare("infection", ModStatusScope.Body, ModDataScope.Shared));
		Assert.True(guestStatus.TryDeclare("infection", ModStatusScope.Body, ModDataScope.Shared));

		Assert.True(TransportOf(host).TryBroadcastBodyStatus("infection", HostId, [1, 2]));
		Assert.True(hostStatus.TryGetBodyStatus("infection", HostId, out var hostValue));
		Assert.Equal([1, 2], hostValue);
		Assert.True(guestStatus.TryGetBodyStatus("infection", HostId, out var guestValue));
		Assert.Equal([1, 2], guestValue);

		Assert.Contains(SyncMod(host).Received, r => r.Sender == HostId && r.Consumed);
		Assert.Contains(SyncMod(guest).Received, r => r.Sender == HostId && r.Consumed);
	}

	[Fact]
	public void SharedBodyStatus_HostBroadcastRemove_ClearsGuestMirrorButKeepsDeclaration()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostStatus = StatusOf(host);
		var guestStatus = StatusOf(guest);

		Assert.True(hostStatus.TryDeclare("infection", ModStatusScope.Body, ModDataScope.Shared));
		Assert.True(guestStatus.TryDeclare("infection", ModStatusScope.Body, ModDataScope.Shared));
		Assert.True(TransportOf(host).TryBroadcastBodyStatus("infection", HostId, [1, 2]));
		Assert.True(guestStatus.TryGetBodyStatus("infection", HostId, out _));

		Assert.True(TransportOf(host).TryBroadcastRemoveBodyStatus("infection", HostId));
		Assert.False(hostStatus.TryGetBodyStatus("infection", HostId, out _));
		Assert.False(guestStatus.TryGetBodyStatus("infection", HostId, out _));
		Assert.Contains("infection", guestStatus.StatusIds);
		Assert.True(guestStatus.TryGetRuntimeScope("infection", out var runtimeScope));
		Assert.Equal(ModDataScope.Shared, runtimeScope);
	}

	[Fact]
	public void SharedLimbStatus_HostBroadcastSetAndRemove_AppliesToGuestLimbMirror()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostStatus = StatusOf(host);
		var guestStatus = StatusOf(guest);

		Assert.True(hostStatus.TryDeclare("limb.bleed", ModStatusScope.Limb, ModDataScope.Shared));
		Assert.True(guestStatus.TryDeclare("limb.bleed", ModStatusScope.Limb, ModDataScope.Shared));

		Assert.True(TransportOf(host).TryBroadcastLimbStatus("limb.bleed", HostId, 2, [7, 8]));
		Assert.True(guestStatus.TryGetLimbStatus("limb.bleed", HostId, 2, out var guestValue));
		Assert.Equal([7, 8], guestValue);

		Assert.True(TransportOf(host).TryBroadcastRemoveLimbStatus("limb.bleed", HostId, 2));
		Assert.False(guestStatus.TryGetLimbStatus("limb.bleed", HostId, 2, out _));
	}

	[Fact]
	public void Broadcast_RefusesNonSharedScopes()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostStatus = StatusOf(host);

		Assert.True(hostStatus.TryDeclare("local.status", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.True(hostStatus.TryDeclare("host.secret", ModStatusScope.Body, ModDataScope.HostAuthoritative));

		Assert.False(TransportOf(host).TryBroadcastBodyStatus("local.status", HostId, [1]));
		Assert.False(TransportOf(host).TryBroadcastBodyStatus("host.secret", HostId, [1]));
		Assert.False(hostStatus.TryGetBodyStatus("local.status", HostId, out _));
	}

	[Fact]
	public void GuestCannotBroadcastSharedStatus()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		Assert.False(TransportOf(guest).TryBroadcastBodyStatus("infection", GuestId, [1]));
		Assert.False(TransportOf(guest).TryBroadcastRemoveBodyStatus("infection", GuestId));
	}

	[Fact]
	public void TryHandleStatusPayload_ReturnsFalseForNonStatusPayload()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		Assert.False(TransportOf(guest).TryHandleStatusPayload(HostId, [1, 2, 3]));
	}
}
