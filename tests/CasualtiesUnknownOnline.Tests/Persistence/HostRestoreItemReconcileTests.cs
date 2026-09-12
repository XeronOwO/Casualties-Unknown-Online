using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// A mid-run restore rebuilds the layer on the host's own side too: the game
/// regenerates the layer from the restored baseline, so the objects it creates
/// are the SAME physical objects the cut described — they must be reconciled
/// against the restored item set, never published beside it under a fresh id.
///
/// The decision lives in the Runtime (it owns the restored set); the world write
/// is the adapter's (see <c>GeneratedItemAuthority</c>/<c>GeneratedItemApplication</c>).
/// This suite pins the Runtime half with the shipped services.
/// </summary>
[Trait("Category", "Integration")]
public sealed class HostRestoreItemReconcileTests
{
	private const ulong HostId = 1001;

	[Fact]
	public void ARegeneratedItemAtARestoredItemsSpot_DoesNotBecomeASecondWorldItem()
	{
		using var world = ItemSimWorld.Create();
		var kernel = world.Host.Services.GetRequiredService<ItemKernelAuthority>();
		RestoreOneShell(kernel, id: 100, x: 5f, y: 5f);

		// The layer is regenerated: the same physical shell exists again at the spot
		// the generation put it, and the adapter publishes the generation set. The
		// restored set is the truth for this world, so the regenerated copy is the
		// SAME item — not a second one.
		world.Items.PublishGeneratedItems(
		[
			new WorldItem(200, Shell(), new NetVector2(5f, 5f), NetVector2.Zero, 0, 0f, false),
		]);

		var worldItems = WorldItems(kernel);
		Assert.Single(worldItems);
		Assert.Equal(100UL, worldItems[0].Identity.InstanceId);
	}

	[Fact]
	public void ANormalGeneration_StillPublishesItsItems()
	{
		using var world = ItemSimWorld.Create();
		var kernel = world.Host.Services.GetRequiredService<ItemKernelAuthority>();

		world.Items.PublishGeneratedItems(
		[
			new WorldItem(200, Shell(), new NetVector2(5f, 5f), NetVector2.Zero, 0, 0f, false),
		]);

		Assert.True(world.Items.IsWorldItemRegistered(200));
		Assert.Single(WorldItems(kernel));
	}

	private static void RestoreOneShell(ItemKernelAuthority kernel, ulong id, float x, float y)
	{
		Assert.True(kernel.TrySpawn(
			HostId,
			new ItemIdentity(id, "shell"),
			ItemLocation.World(x, y),
			Shell(),
			out _,
			out _));
		Assert.True(kernel.Restore(kernel.CreateCheckpoint()).Success);
	}

	private static List<ItemState> WorldItems(ItemKernelAuthority kernel) =>
		[.. kernel.QueryItems().Values.Where(item => item.Location.Kind == ItemLocationKind.World)];

	private static CharacterItemMsg Shell() => new() { ItemId = "shell" };
}
