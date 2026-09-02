using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The runtime mod-data seam: a process-local, scope-declared store with
/// explicit shared-mirror application and no automatic snapshot protocol.
/// Tests cover local-only independence, shared host-write/guest-apply,
/// host-authoritative host-only visibility, scope validation by network mode,
/// defensive copies, and policy caps.
/// </summary>
public class ModDataTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static IModData DataOf(TestNode node) =>
		((TestDataMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestDataMod)).Context!.Data;

	private static IModData ClientOnlyDataOf(TestNode node) =>
		((TestClientOnlyDataMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestClientOnlyDataMod)).Context!.Data;

	[Fact]
	public void LocalOnly_AnyRoleCanReadWriteRemove_AndCopiesAreDefensive()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var data = DataOf(host);

		Assert.True(data.TryDeclare("local", ModDataScope.LocalOnly));
		Assert.True(data.TryGetScope("local", out var scope));
		Assert.Equal(ModDataScope.LocalOnly, scope);
		Assert.True(data.TryGetSchemaVersion("local", out var schemaVersion));
		Assert.Equal(1, schemaVersion);

		var original = new byte[] { 1, 2, 3 };
		Assert.True(data.TrySet("local", original));
		original[0] = 9; // caller mutation must not leak into the store

		Assert.True(data.TryGet("local", out var firstRead));
		firstRead![1] = 8; // caller mutation of the returned copy must not leak either

		Assert.True(data.TryGet("local", out var secondRead));
		Assert.Equal([1, 2, 3], secondRead);

		Assert.Single(data.Keys);
		Assert.Contains("local", data.Keys);
		Assert.Equal(1, data.Count);

		Assert.True(data.TryRemove("local"));
		Assert.False(data.TryGet("local", out _));
		Assert.Empty(data.Keys);
		Assert.Equal(0, data.Count);
	}

	[Fact]
	public void LocalOnly_IsIndependentBetweenHostAndGuest()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostData = DataOf(host);
		var guestData = DataOf(guest);

		Assert.True(hostData.TryDeclare("local", ModDataScope.LocalOnly));
		Assert.True(guestData.TryDeclare("local", ModDataScope.LocalOnly));
		Assert.True(hostData.TrySet("local", [1]));
		Assert.True(guestData.TrySet("local", [2]));

		Assert.True(hostData.TryGet("local", out var hostValue));
		Assert.True(guestData.TryGet("local", out var guestValue));
		Assert.Equal([1], hostValue);
		Assert.Equal([2], guestValue);
	}

	[Fact]
	public void Shared_HostWritesGuestApplies_GuestCannotWriteOrRemove()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostData = DataOf(host);
		var guestData = DataOf(guest);

		Assert.True(hostData.TryDeclare("shared", ModDataScope.Shared));
		Assert.True(guestData.TryDeclare("shared", ModDataScope.Shared));
		Assert.True(hostData.TrySet("shared", [1, 2]));

		// Guests cannot mutate the authoritative value; they must request through commands/messages.
		Assert.False(guestData.TrySet("shared", [9]));
		Assert.False(guestData.TryGet("shared", out _), "the guest has no mirror until it applies a host value.");
		Assert.False(guestData.TryRemove("shared"));

		// A host-originated value may be applied to the local mirror.
		Assert.True(guestData.TryApplyShared("shared", [3, 4], HostId));
		Assert.True(guestData.TryGet("shared", out var guestMirror));
		Assert.Equal([3, 4], guestMirror);

		// Non-host senders and host-side apply are refused.
		Assert.False(guestData.TryApplyShared("shared", [5], GuestId));
		Assert.False(hostData.TryApplyShared("shared", [6], HostId));

		// The host's authoritative value remains unchanged by guest mirror applies.
		Assert.True(hostData.TryGet("shared", out var hostValue));
		Assert.Equal([1, 2], hostValue);

		// The host can remove the slot.
		Assert.True(hostData.TryRemove("shared"));
		Assert.False(hostData.TryGet("shared", out _));
	}

	[Fact]
	public void HostAuthoritative_IsVisibleOnlyOnHostAndCannotBeAppliedByGuests()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostData = DataOf(host);
		var guestData = DataOf(guest);

		Assert.True(hostData.TryDeclare("host", ModDataScope.HostAuthoritative));
		Assert.True(guestData.TryDeclare("host", ModDataScope.HostAuthoritative));
		Assert.True(hostData.TrySet("host", [7, 8]));

		Assert.True(hostData.TryGet("host", out var hostValue));
		Assert.Equal([7, 8], hostValue);
		Assert.True(hostData.TryGetScope("host", out var hostScope));
		Assert.Equal(ModDataScope.HostAuthoritative, hostScope);

		// The framework keeps no guest mirror for host-authoritative data.
		Assert.False(guestData.TryGet("host", out _));
		Assert.False(guestData.TryGetScope("host", out _));
		Assert.False(guestData.TryApplyShared("host", [9], HostId));
		Assert.False(guestData.TryRemove("host"));
		Assert.Empty(guestData.Keys);
		Assert.Equal(0, guestData.Count);

		Assert.Single(hostData.Keys);
		Assert.Equal(1, hostData.Count);
	}

	[Fact]
	public void Declaration_IsRequiredAndDuplicateOrInvalidDeclarationsAreRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var data = DataOf(host);

		Assert.False(data.TrySet("missing", [1]), "writing an undeclared slot must be refused.");
		Assert.False(data.TryGet("missing", out _));
		Assert.False(data.TryGetScope("missing", out _));

		Assert.True(data.TryDeclare("local", ModDataScope.LocalOnly));
		Assert.True(data.TryDeclare("versioned", ModDataScope.LocalOnly, schemaVersion: 3));
		Assert.True(data.TryGetSchemaVersion("versioned", out var versioned));
		Assert.Equal(3, versioned);
		Assert.False(data.TryDeclare("local", ModDataScope.LocalOnly), "duplicate declarations are refused.");
		Assert.False(data.TryDeclare("", ModDataScope.LocalOnly), "empty keys are refused.");
		Assert.False(data.TryDeclare("bad-schema", ModDataScope.LocalOnly, schemaVersion: 0), "non-positive schema versions are refused.");
	}

	[Fact]
	public void ValueCaps_AreEnforcedWithoutSilentTruncation()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var data = DataOf(host);

		Assert.True(data.TryDeclare("local", ModDataScope.LocalOnly));
		Assert.False(data.TrySet("local", new byte[64 * 1024 + 1]), "over-cap values are refused.");
		Assert.True(data.TrySet("local", []), "empty values are legal.");
		Assert.True(data.TryGet("local", out var empty));
		Assert.Empty(empty!);
	}

	[Fact]
	public void ClientOnly_CannotDeclareSharedOrHostAuthoritative()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var data = ClientOnlyDataOf(host);

		Assert.True(data.TryDeclare("local", ModDataScope.LocalOnly));
		Assert.False(data.TryDeclare("shared", ModDataScope.Shared), "ClientOnly has no state-bearing handshake.");
		Assert.False(data.TryDeclare("host", ModDataScope.HostAuthoritative), "ClientOnly has no host-authoritative runtime slot.");
	}
}
