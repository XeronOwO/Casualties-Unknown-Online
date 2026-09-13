using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The layer boundary's LAYER-SCOPED kernel reset (decision 174): the enemy rows and
/// the fluid chunks describe the layer being LEFT, so the generation boundary must drop
/// them — the same rule that already drops the world-rooted items and the world-entity
/// facts. A row that survived would be handed to a late joiner's checkpoint, and the
/// host re-allocates enemy ids per layer, so the surviving row's id can be re-minted for
/// a DIFFERENT enemy.
///
/// The negative half is as binding as the positive one: the PLAYER table must survive
/// the boundary, because <see cref="PlayerState"/> carries the cross-layer terminal
/// facts (alive/conscious, the carry relation, the limb latches).
/// </summary>
[Trait("Category", "Integration")]
public class LayerBoundaryKernelResetTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void LayerReset_DropsBothReplacedLayerTableFamilies()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var world = host.Services.GetRequiredService<WorldService>();
		var kernel = host.Services.GetRequiredService<ItemKernelAuthority>();

		Assert.True(kernel.TryUpsertEnemy(
			HostId, new EnemyState(new EntityId(1UL, 7U, 0), "spider", 4f, false, false), out _, out _));
		Assert.True(kernel.TryUpdateFluidRegion(
			HostId, new FluidRegionState(1, 2, 7, 1, 50), out _, out _));

		world.ResetDamagedBlocks();

		Assert.Empty(kernel.QueryEnemies()!.Enemies);
		Assert.Empty(kernel.QueryFluids()!.Regions);
	}

	[Fact]
	public void LayerReset_KeepsTheEnemyTombstones()
	{
		// A tombstone is a terminal fact the killer earned, not a fact about one
		// layer's layout: "the enemy standing here was killed" must keep stopping a
		// stale live row from resurrecting it on the next descent. The acceptance rule
		// is that a killed enemy never comes back, so the layer reset drops the live
		// rows and leaves the tombstones standing.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var world = host.Services.GetRequiredService<WorldService>();
		var kernel = host.Services.GetRequiredService<ItemKernelAuthority>();
		var enemyId = new EntityId(1UL, 7U, 0);

		Assert.True(kernel.TryUpsertEnemy(
			HostId, new EnemyState(enemyId, "spider", 4f, false, false), out _, out _));
		Assert.True(kernel.TryRemoveEnemy(HostId, enemyId, out _, out _));

		world.ResetDamagedBlocks();

		Assert.Empty(kernel.QueryEnemies()!.Enemies);
		Assert.Equal(enemyId, Assert.Single(kernel.QueryEnemies()!.Removed));
	}

	[Fact]
	public void LayerReset_KeepsThePlayerTable()
	{
		// The boundary runs while the bodies cross it. PlayerState holds the durable
		// cross-layer facts, so dropping the table would erase every player's injuries
		// and carry relation on each descent.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var world = host.Services.GetRequiredService<WorldService>();
		var kernel = host.Services.GetRequiredService<ItemKernelAuthority>();

		Assert.True(kernel.TryUpdatePlayerStatus(
			HostId, new PlayerState(HostId, Alive: true, Conscious: true), out _, out _));

		world.ResetDamagedBlocks();

		Assert.Single(kernel.QueryPlayers()!.Players);
	}

	[Fact]
	public void LayerReset_LeavesTheGuestKernelAlone()
	{
		// The reset is host-local by construction: the guest's kernel is the host's
		// replay, so a guest that committed its own reset would diverge from the
		// checkpoint the host sends it at the next world entry. This drives the GUEST's
		// own WorldService — the only path LayerScopedTableReset gates — rather than the
		// host's, which would prove nothing about the gate.
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var guestWorld = guest.Services.GetRequiredService<WorldService>();
		var guestKernel = guest.Services.GetRequiredService<ItemKernelAuthority>();

		Assert.True(guestKernel.TryUpsertEnemy(
			HostId, new EnemyState(new EntityId(1UL, 7U, 0), "spider", 4f, false, false), out _, out _));
		Assert.True(guestKernel.TryUpdateFluidRegion(
			HostId, new FluidRegionState(1, 2, 7, 1, 50), out _, out _));

		guestWorld.ResetDamagedBlocks();

		Assert.Single(guestKernel.QueryEnemies()!.Enemies);
		Assert.Single(guestKernel.QueryFluids()!.Regions);
	}
}
