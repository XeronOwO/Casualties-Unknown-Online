using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The item-instance-id coordination: the per-guest counter watermarks (a
/// crashed-and-rejoined guest's counter restarts from zero and would reuse ids
/// the host's tables still hold — the host records the high-water mark and
/// grants it back on rejoin) and the carried-inventory registration (the
/// guest's self-assigned starting-supply ids, registered in the transfer table
/// so its use/slot reports arbitrate normally).
///
/// The same rule has a second source: a restored cut re-stamps the ARCHIVE's
/// ids onto the objects this process rebuilds, and an id is (counter, id space) —
/// the space being the account id's LOW 32 BITS — so the archive's own rows name
/// the resume point of every id space that played the world, the local one
/// included (its counter also restarts with the process). The restore-driven half
/// is the last three tests, and they use account-sized ids: with ids that fit in
/// 32 bits, "the id space" and "the full account id" are the same number and the
/// defect this suite pins is invisible.
/// </summary>
[Trait("Category", "Integration")]
public class ItemIdCoordinatorTests
{
	[Fact]
	public void GuestWatermark_HighWaterSurvives_RegrantedOnRejoin()
	{
		using var w = ItemSimWorld.Create();

		// The guest allocated up to counter 42, then reported a stale 10 (an
		// out-of-order retransmit) — the high-water mark must survive.
		w.G1.Services.GetRequiredService<IItemControl>().SendItemIdWatermark(42);
		w.Driver.Tick(50);
		w.G1.Services.GetRequiredService<IItemControl>().SendItemIdWatermark(10);
		w.Driver.Tick(50);

		// The guest vanishes from the lobby — the presence check removes it
		// from the host's session after ~2 s (SessionSimulationTests precedent).
		w.Host.Steam.LobbyMembers = [w.Host.SteamId, w.G2.SteamId];
		w.G1.Steam.LobbyMembers = [w.G1.SteamId];
		w.Driver.Tick(2100);
		Assert.False(w.G1.Session.SessionActive, "the guest's session must end when the lobby loses the host");

		// The guest returns: the lobby membership restores, the rejoin flow
		// fires the lobby-entered callback again, the handshake rebuilds —
		// the host grants the RECORDED high-water mark (42), not the stale 10.
		w.Host.Steam.LobbyMembers = [w.Host.SteamId, w.G1.SteamId, w.G2.SteamId];
		w.G1.Steam.LobbyMembers = [w.Host.SteamId, w.G1.SteamId, w.G2.SteamId];
		w.G1.Steam.FireLobbyEntered(9001);
		w.Driver.TickUntil(() => w.Host.Session.Members.Count(m => m.Handshaken) == 2, maxMs: 5000);

		// The rejoin's grant rides the wire (the initial handshake's grant was
		// before the recording surface went up — the list starts clean).
		var grants = w.Watermarks(w.G1);
		Assert.True(grants.Count == 1, $"the rejoin must grant the recorded watermark once, got {grants.Count}");
		Assert.True(grants[0].Counter == 42, $"the grant must carry the high-water mark (42), got {grants[0].Counter}");
	}

	[Fact]
	public void CarriedInventory_RegistersInTheTransferTable()
	{
		using var w = ItemSimWorld.Create();

		w.G1.Services.GetRequiredService<IItemControl>().SendCarriedInventory(
		[
			new CharacterItemMsg { InstanceId = 101, ItemId = "ore", Condition = 1f },
			new CharacterItemMsg { InstanceId = 102, ItemId = "medkit", Condition = 0.5f },
		]);
		w.Driver.Tick(50);

		Assert.True(w.TransferredOf(w.G1, 101), "the first starting supply registered in the transfer table");
		Assert.True(w.TransferredOf(w.G1, 102), "the second starting supply registered in the transfer table");
	}

	[Fact]
	public void RestoredCut_ResumesTheLocalAllocator_AboveTheIdsTheArchiveReissued()
	{
		using var w = CreateAccountSizedWorld();
		var kernel = w.Host.Services.GetRequiredService<ItemKernelAuthority>();
		var resumes = new List<ulong>();
		w.Host.Services.GetRequiredService<IItemControl>().ItemIdWatermarkReceived += resumes.Add;

		// The previous process handed the local id space counters 0 and 7, and a guest's
		// space its own 9: the archive carries all three rows, and a restore re-stamps
		// them onto the objects this process rebuilds while its own counter starts at zero.
		Assert.True(Spawn(kernel, w.Host.SteamId, InstanceId(0, w.Host.SteamId), x: 5f));
		Assert.True(Spawn(kernel, w.Host.SteamId, InstanceId(7, w.Host.SteamId), x: 6f));
		Assert.True(Spawn(kernel, w.G1.SteamId, InstanceId(9, w.G1.SteamId), x: 7f));
		Assert.True(kernel.Restore(kernel.CreateCheckpoint()).Success);

		// The local resume point is the LOCAL space's high-water mark (7), not the archive's
		// overall maximum (9, another account's space): reading the wrong space would resume
		// this allocator from a counter that is not its own.
		Assert.Equal([7UL], resumes);
	}

	[Fact]
	public void RestoredCut_RaisesTheWatermarkOfAMemberAlreadyInTheSession()
	{
		using var w = CreateAccountSizedWorld();
		var kernel = w.Host.Services.GetRequiredService<ItemKernelAuthority>();

		// The archive holds a guest-owned id at counter 9 (that guest's previous process)
		// and the host's own at counter 3; the guest is in the lobby while the host
		// continues the world.
		Assert.True(Spawn(kernel, w.G1.SteamId, InstanceId(9, w.G1.SteamId), x: 5f));
		Assert.True(Spawn(kernel, w.Host.SteamId, InstanceId(3, w.Host.SteamId), x: 6f));
		Assert.True(kernel.Restore(kernel.CreateCheckpoint()).Success);

		// The character this guest is about to receive carries those ids, so the raised
		// resume point has to reach it now — a present member sees no second handshake.
		// The other member's space holds nothing from the archive, so it is left alone.
		var grants = w.Watermarks(w.G1);
		Assert.Equal(9UL, Assert.Single(grants).Counter);
		Assert.Empty(w.Watermarks(w.G2));
	}

	[Fact]
	public void RestoredCut_RaisesTheWatermarkOfAnAbsentMember_AndASessionBoundaryKeepsIt()
	{
		using var w = CreateAccountSizedWorld();
		var kernel = w.Host.Services.GetRequiredService<ItemKernelAuthority>();

		// The guest allocated up to counter 3 and then vanished from the lobby.
		w.G1.Services.GetRequiredService<IItemControl>().SendItemIdWatermark(3);
		w.Driver.Tick(50);
		w.Host.Steam.LobbyMembers = [w.Host.SteamId, w.G2.SteamId];
		w.G1.Steam.LobbyMembers = [w.G1.SteamId];
		w.Driver.Tick(2100);
		Assert.False(w.G1.Session.SessionActive, "the guest's session must end when the lobby loses the host");

		// The host continues a world whose archive holds that guest's ids up to
		// counter 9 — the ones its restored character will come back wearing.
		Assert.True(Spawn(kernel, w.G1.SteamId, InstanceId(9, w.G1.SteamId), x: 5f));
		Assert.True(kernel.Restore(kernel.CreateCheckpoint()).Success);

		// A session boundary is not a world boundary: the mark the archive put in belongs to
		// the world those ids live in — a host may continue the same world in a new lobby —
		// so the reset that ends a session must not forget it. Forgetting it is how a member
		// whose process restarted is granted 0 and hands out ids the world already holds.
		w.Items.ResetSessionState();

		// The guest returns: the grant must carry the restored high-water mark (9), not the
		// 3 it reported before it left, and not 0.
		w.Host.Steam.LobbyMembers = [w.Host.SteamId, w.G1.SteamId, w.G2.SteamId];
		w.G1.Steam.LobbyMembers = [w.Host.SteamId, w.G1.SteamId, w.G2.SteamId];
		w.G1.Steam.FireLobbyEntered(9001);
		w.Driver.TickUntil(() => w.Host.Session.Members.Count(m => m.Handshaken) == 2, maxMs: 5000);

		var grants = w.Watermarks(w.G1);
		Assert.Equal(9UL, grants[grants.Count - 1].Counter);
	}

	/// <summary>
	/// The format's own contract, because two mechanisms now read it: the space key is the
	/// account id's low 32 bits (never the account id itself — that is the comparison a
	/// resume gets wrong), an id hands that key back as its owner half, and the synthesized
	/// namespace is marked so a plain resume can tell it apart.
	/// </summary>
	[Fact]
	public void TheIdSpaceKey_IsTheAccountIdsLowBits_AndTheSynthesizedNamespaceIsMarked()
	{
		Assert.Equal(HostAccount & 0xFFFFFFFFUL, ItemInstanceIds.SpaceOf(HostAccount));
		Assert.NotEqual(HostAccount, ItemInstanceIds.SpaceOf(HostAccount));

		var id = ItemInstanceIds.Compose(7, HostAccount);
		Assert.Equal(7UL, ItemInstanceIds.CounterOf(id));
		Assert.Equal(ItemInstanceIds.SpaceOf(HostAccount), ItemInstanceIds.OwnerOf(id));
		Assert.False(ItemInstanceIds.IsAwarded(id));

		Assert.True(ItemInstanceIds.IsAwarded(ItemInstanceIds.AwardedHighBit | ItemInstanceIds.SpaceOf(HostAccount)));
	}

	/// <summary>Account-sized ids: an id's owner half is its account id's LOW 32 bits, so a world whose ids fit in 32 bits cannot tell the two apart — the defect this suite pins hid behind exactly that.</summary>
	private const ulong HostAccount = 76561198000001001UL;

	private const ulong Guest1Account = 76561198000002001UL;

	private const ulong Guest2Account = 76561198000003001UL;

	private static ItemSimWorld CreateAccountSizedWorld() =>
		ItemSimWorld.Create(hostSteamId: HostAccount, guest1SteamId: Guest1Account, guest2SteamId: Guest2Account);

	/// <summary>One restored item row: the kernel table is what a restore re-issues ids from.</summary>
	private static bool Spawn(ItemKernelAuthority kernel, ulong actor, ulong instanceId, float x) =>
		kernel.TrySpawn(actor, new ItemIdentity(instanceId, "shell"), ItemLocation.World(x, x), Shell(), out _, out _);

	private static CharacterItemMsg Shell() => new() { ItemId = "shell" };

	/// <summary>The id format the claim is about, stated here independently of the production helper: (counter, account-low).</summary>
	private static ulong InstanceId(ulong counter, ulong accountId) => (counter << 32) | (uint)accountId;
}
