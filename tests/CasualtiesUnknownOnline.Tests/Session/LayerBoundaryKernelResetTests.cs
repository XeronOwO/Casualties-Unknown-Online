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
	public void LayerReset_AnEmptyEnemyTable_CommitsNoEnemyReset()
	{
		// A layer being entered can have fluid chunks and NO captured enemies — a
		// run's FIRST layer, where the host does not capture while the world
		// generates. The reset must not drag the empty enemy table through it: an
		// enemy reset commits an `EnemiesResetEvent`, and that event tells every
		// member to drop the bindings and adoption candidates of the layer being
		// ENTERED (batch `20261021-f`, row 1: the members were still reset at entry
		// and only the scene-scan adoption candidates saved the pass).
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var world = host.Services.GetRequiredService<WorldService>();
		var kernel = host.Services.GetRequiredService<ItemKernelAuthority>();

		Assert.True(kernel.TryUpdateFluidRegion(
			HostId, new FluidRegionState(1, 2, 7, 1, 50), out _, out _));

		var enemyResets = 0;
		kernel.BatchCommitted += batch =>
		{
			foreach (var @event in batch.Events)
			{
				if (@event is EnemiesResetEvent)
				{
					enemyResets++;
				}
			}
		};

		world.ResetDamagedBlocks();

		Assert.Equal(0, enemyResets);
		Assert.Empty(kernel.QueryFluids()!.Regions); // the fluid half still ran
	}

	[Fact]
	public void LayerReset_KeepsTheEnemyTombstones()
	{
		// A tombstone is a terminal fact the killer earned, not a fact about one
		// layer's layout: "the enemy standing here was killed" must keep stopping a
		// stale live row from resurrecting it on the next descent. The acceptance rule
		// is that a killed enemy never comes back, so the layer reset drops the live
		// rows and leaves the tombstones standing. A LIVE row rides along: without
		// one, an empty live table is (correctly) not reset at all and this case
		// would pass without ever exercising the reset.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var world = host.Services.GetRequiredService<WorldService>();
		var kernel = host.Services.GetRequiredService<ItemKernelAuthority>();
		var removedId = new EntityId(1UL, 7U, 0);
		var liveId = new EntityId(1UL, 8U, 0);

		Assert.True(kernel.TryUpsertEnemy(
			HostId, new EnemyState(removedId, "spider", 4f, false, false), out _, out _));
		Assert.True(kernel.TryRemoveEnemy(HostId, removedId, out _, out _));
		Assert.True(kernel.TryUpsertEnemy(
			HostId, new EnemyState(liveId, "shadecrawler", 4f, false, false), out _, out _));

		world.ResetDamagedBlocks();

		Assert.Empty(kernel.QueryEnemies()!.Enemies);
		Assert.Equal(removedId, Assert.Single(kernel.QueryEnemies()!.Removed));
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
