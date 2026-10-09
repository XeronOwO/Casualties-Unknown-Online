using static CasualtiesUnknownOnline.Tests.Mods.ModValues;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The runtime status domain phase-1 seam: a per-mod, per-player, optional-limb
/// ephemeral status store with local/shared/host-authoritative scope rules and
/// explicit shared application. It deliberately has no vanilla Body/Limb
/// integration and no automatic sync.
/// </summary>
[Trait("Category", "Integration")]
public class ModStatusRuntimeTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static IModStatusRuntime StatusOf(TestNode node) =>
		((TestDataMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestDataMod)).Context!.StatusRuntime;

	private static IModStatusRuntime ClientOnlyStatusOf(TestNode node) =>
		((TestClientOnlyDataMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestClientOnlyDataMod)).Context!.StatusRuntime;

	[Fact]
	public void LocalOnly_BodyStatus_AnyRoleCanReadWriteRemove_AndCopiesAreDefensive()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var status = StatusOf(host);

		Assert.True(status.TryDeclare("poison", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.True(status.TryGetScope("poison", out var scope));
		Assert.Equal(ModStatusScope.Body, scope);
		Assert.True(status.TryGetRuntimeScope("poison", out var runtimeScope));
		Assert.Equal(ModDataScope.LocalOnly, runtimeScope);
		Assert.True(status.TryGetSchemaVersion("poison", out var schemaVersion));
		Assert.Equal(1, schemaVersion);

		var original = Ints(1, 2, 3);
		Assert.True(status.TrySetBodyStatus("poison", HostId, original));

		// A value is immutable, so nothing on either side of the store can change
		// what it keeps: the two reads are equal, and they are the same value —
		// the defensive copy every byte-shaped surface needed is gone with the
		// bytes, not merely moved.
		Assert.True(status.TryGetBodyStatus("poison", HostId, out var firstRead));
		Assert.True(status.TryGetBodyStatus("poison", HostId, out var secondRead));
		Assert.Equal(original, firstRead);
		Assert.Same(firstRead, secondRead);

		Assert.Single(status.StatusIds);
		Assert.Equal(1, status.StatusCount);

		Assert.True(status.TryRemoveBodyStatus("poison", HostId));
		Assert.False(status.TryGetBodyStatus("poison", HostId, out _));
	}

	[Fact]
	public void LocalOnly_BodyStatus_IsIndependentBetweenHostAndGuest()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostStatus = StatusOf(host);
		var guestStatus = StatusOf(guest);

		Assert.True(hostStatus.TryDeclare("poison", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.True(guestStatus.TryDeclare("poison", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.True(hostStatus.TrySetBodyStatus("poison", HostId, Ints(1)));
		Assert.True(guestStatus.TrySetBodyStatus("poison", GuestId, Ints(2)));

		Assert.True(hostStatus.TryGetBodyStatus("poison", HostId, out var hostValue));
		Assert.True(guestStatus.TryGetBodyStatus("poison", GuestId, out var guestValue));
		Assert.Equal(Ints(1), hostValue);
		Assert.Equal(Ints(2), guestValue);
	}

	[Fact]
	public void Shared_BodyStatus_HostWritesGuestApplies_GuestCannotWriteOrRemove()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostStatus = StatusOf(host);
		var guestStatus = StatusOf(guest);

		Assert.True(hostStatus.TryDeclare("infection", ModStatusScope.Body, ModDataScope.Shared));
		Assert.True(guestStatus.TryDeclare("infection", ModStatusScope.Body, ModDataScope.Shared));
		Assert.True(hostStatus.TrySetBodyStatus("infection", HostId, Ints(1, 2)));

		Assert.False(guestStatus.TrySetBodyStatus("infection", GuestId, Ints(9)));
		Assert.False(guestStatus.TryGetBodyStatus("infection", HostId, out _), "guest has no mirror until it applies.");
		Assert.False(guestStatus.TryRemoveBodyStatus("infection", HostId));

		Assert.True(guestStatus.TryApplyBodyStatus("infection", HostId, Ints(3, 4), HostId));
		Assert.True(guestStatus.TryGetBodyStatus("infection", HostId, out var guestMirror));
		Assert.Equal(Ints(3, 4), guestMirror);

		Assert.False(guestStatus.TryApplyBodyStatus("infection", HostId, Ints(5), GuestId));
		Assert.False(hostStatus.TryApplyBodyStatus("infection", HostId, Ints(6), HostId));

		Assert.True(hostStatus.TryGetBodyStatus("infection", HostId, out var hostValue));
		Assert.Equal(Ints(1, 2), hostValue);

		Assert.True(hostStatus.TryRemoveBodyStatus("infection", HostId));
		Assert.False(hostStatus.TryGetBodyStatus("infection", HostId, out _));
	}

	[Fact]
	public void Shared_BodyStatus_GuestCanApplyHostRemoval_AndRefusesNonHostRemoval()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostStatus = StatusOf(host);
		var guestStatus = StatusOf(guest);

		Assert.True(hostStatus.TryDeclare("infection", ModStatusScope.Body, ModDataScope.Shared));
		Assert.True(guestStatus.TryDeclare("infection", ModStatusScope.Body, ModDataScope.Shared));
		Assert.True(hostStatus.TrySetBodyStatus("infection", HostId, Ints(1, 2)));
		Assert.True(guestStatus.TryApplyBodyStatus("infection", HostId, Ints(3, 4), HostId));

		// A host-originated removal clears the guest mirror, not the declaration.
		Assert.True(guestStatus.TryApplyRemoveBodyStatus("infection", HostId, HostId));
		Assert.False(guestStatus.TryGetBodyStatus("infection", HostId, out _));
		Assert.True(guestStatus.TryGetRuntimeScope("infection", out var runtimeScope));
		Assert.Equal(ModDataScope.Shared, runtimeScope);

		// Re-set for the rejection case.
		Assert.True(guestStatus.TryApplyBodyStatus("infection", HostId, Ints(3, 4), HostId));
		Assert.False(guestStatus.TryApplyRemoveBodyStatus("infection", HostId, GuestId), "a guest must not remove with its own id as sender.");
		Assert.True(guestStatus.TryGetBodyStatus("infection", HostId, out _));

		Assert.False(hostStatus.TryApplyRemoveBodyStatus("infection", HostId, HostId), "the host does not apply mirrors.");
	}

	[Fact]
	public void HostAuthoritative_BodyStatus_IsVisibleOnlyOnHost()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostStatus = StatusOf(host);
		var guestStatus = StatusOf(guest);

		Assert.True(hostStatus.TryDeclare("secret", ModStatusScope.Body, ModDataScope.HostAuthoritative));
		Assert.True(guestStatus.TryDeclare("secret", ModStatusScope.Body, ModDataScope.HostAuthoritative));
		Assert.True(hostStatus.TrySetBodyStatus("secret", HostId, Ints(7, 8)));

		Assert.True(hostStatus.TryGetBodyStatus("secret", HostId, out var hostValue));
		Assert.Equal(Ints(7, 8), hostValue);
		Assert.Single(hostStatus.StatusIds);
		Assert.Equal(1, hostStatus.StatusCount);

		Assert.False(guestStatus.TryGetBodyStatus("secret", HostId, out _));
		Assert.False(guestStatus.TryGetScope("secret", out _));
		Assert.False(guestStatus.TryApplyBodyStatus("secret", HostId, Ints(9), HostId));
		Assert.False(guestStatus.TryRemoveBodyStatus("secret", HostId));
		Assert.Empty(guestStatus.StatusIds);
		Assert.Equal(0, guestStatus.StatusCount);
	}

	[Fact]
	public void LimbStatus_RequiresLimbDeclarationAndValidSlot()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var status = StatusOf(host);

		Assert.True(status.TryDeclare("limb.bleed", ModStatusScope.Limb, ModDataScope.LocalOnly));
		Assert.True(status.TrySetLimbStatus("limb.bleed", HostId, 2, Ints(1, 2)));
		Assert.True(status.TryGetLimbStatus("limb.bleed", HostId, 2, out var value));
		Assert.Equal(Ints(1, 2), value);

		// A body-scoped status cannot be read/written through the limb API.
		Assert.True(status.TryDeclare("body.only", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.False(status.TryGetLimbStatus("body.only", HostId, 0, out _));
		Assert.False(status.TrySetLimbStatus("body.only", HostId, 0, Ints(1)));

		// A limb-scoped status cannot be read/written through the body API.
		Assert.False(status.TryGetBodyStatus("limb.bleed", HostId, out _));
		Assert.False(status.TrySetBodyStatus("limb.bleed", HostId, Ints(1)));

		// Invalid limb slots are refused.
		Assert.False(status.TrySetLimbStatus("limb.bleed", HostId, -1, Ints(1)));
		Assert.False(status.TrySetLimbStatus("limb.bleed", HostId, 256, Ints(1)));

		Assert.True(status.TryRemoveLimbStatus("limb.bleed", HostId, 2));
		Assert.False(status.TryGetLimbStatus("limb.bleed", HostId, 2, out _));
	}

	[Fact]
	public void Declaration_IsRequiredAndInvalidDeclarationsAreRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var status = StatusOf(host);

		Assert.False(status.TrySetBodyStatus("missing", HostId, Ints(1)), "writing an undeclared status must be refused.");
		Assert.False(status.TryGetBodyStatus("missing", HostId, out _));

		Assert.True(status.TryDeclare("poison", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.False(status.TryDeclare("poison", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.False(status.TryDeclare("", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.False(status.TryDeclare("bad-schema", ModStatusScope.Body, ModDataScope.LocalOnly, schemaVersion: 0));

		var overCap = OverCap();
		Assert.True(status.TryDeclare("capped", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.False(status.TrySetBodyStatus("capped", HostId, overCap), "over-cap values are refused.");
	}

	[Fact]
	public void ClientOnly_CannotDeclareSharedOrHostAuthoritativeStatus()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var status = ClientOnlyStatusOf(host);

		Assert.True(status.TryDeclare("local", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.False(status.TryDeclare("shared", ModStatusScope.Body, ModDataScope.Shared));
		Assert.False(status.TryDeclare("host", ModStatusScope.Body, ModDataScope.HostAuthoritative));
	}
}
