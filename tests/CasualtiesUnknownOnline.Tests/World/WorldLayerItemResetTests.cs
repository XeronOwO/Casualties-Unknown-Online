using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The layer boundary resets the item domain together with its sibling domains
/// (world entities, players, enemies, fluids). The previous layer's world items
/// are gone with the old scene, so the authoritative kernel must drop them:
/// otherwise they accumulate across layers on the host, and a mid-run cut taken
/// later writes the old layers' items into the archive (the reconcile then
/// materializes them into the regenerated world).
///
/// Carried items are NOT world-rooted: the player carries them across the layer
/// boundary, so they and their container contents survive the reset.
/// </summary>
[Trait("Category", "Integration")]
public class WorldLayerItemResetTests
{
	private const ulong HostId = 1001;

	[Fact]
	public void NewLayer_DropsThePreviousLayersWorldItems()
	{
		using var w = ItemSimWorld.Create();
		var kernel = Kernel(w.Host);
		SpawnWorldItem(kernel, 100, 5f, 5f);
		Assert.NotNull(kernel.FindItem(100)); // precondition: the old layer holds the item

		w.Host.Services.GetRequiredService<IWorldControl>().ResetDamagedBlocks(); // a new layer is generating

		Assert.Null(kernel.FindItem(100));
	}

	[Fact]
	public void NewLayer_DropsTheContentsOfAWorldContainer()
	{
		using var w = ItemSimWorld.Create();
		var kernel = Kernel(w.Host);
		SpawnWorldItem(kernel, 100, 5f, 5f);
		Assert.True(kernel.TrySpawn(
			HostId,
			new ItemIdentity(101, "shell"),
			ItemLocation.Contained(new ActorId(HostId), 100),
			Shell(),
			out _,
			out _));

		w.Host.Services.GetRequiredService<IWorldControl>().ResetDamagedBlocks();

		Assert.Null(kernel.FindItem(100));
		Assert.Null(kernel.FindItem(101)); // the child is world-rooted through its container
	}

	[Fact]
	public void NewLayer_KeepsTheCarriedItemsAndTheirContents()
	{
		using var w = ItemSimWorld.Create();
		var kernel = Kernel(w.Host);
		Assert.True(kernel.TrySpawnCarried(HostId, 100, "backpack", Shell("backpack"), out _, out _));
		Assert.True(kernel.TrySpawn(
			HostId,
			new ItemIdentity(101, "shell"),
			ItemLocation.Contained(new ActorId(HostId), 100),
			Shell(),
			out _,
			out _));
		SpawnWorldItem(kernel, 200, 7f, 7f);

		w.Host.Services.GetRequiredService<IWorldControl>().ResetDamagedBlocks();

		Assert.NotNull(kernel.FindItem(100)); // carried across the boundary
		Assert.NotNull(kernel.FindItem(101)); // and so are its contents
		Assert.Null(kernel.FindItem(200)); // the world item is gone with the old scene
	}

	[Fact]
	public void NewLayer_ReachesTheGuests()
	{
		using var w = ItemSimWorld.Create();
		var hostKernel = Kernel(w.Host);
		var guestKernel = Kernel(w.G1);
		SpawnWorldItem(hostKernel, 100, 5f, 5f);
		w.Driver.Tick(50);
		Assert.NotNull(guestKernel.FindItem(100)); // precondition: the guest applied the layer item

		w.Host.Services.GetRequiredService<IWorldControl>().ResetDamagedBlocks();
		w.Driver.Tick(50);

		Assert.Null(hostKernel.FindItem(100));
		Assert.Null(guestKernel.FindItem(100)); // the reset travels as a committed batch
	}

	[Fact]
	public void NewLayer_ResetsAnEmptyTableWithoutFaulting()
	{
		using var w = ItemSimWorld.Create();

		w.Host.Services.GetRequiredService<IWorldControl>().ResetDamagedBlocks();
		w.Host.Services.GetRequiredService<IWorldControl>().ResetDamagedBlocks();

		Assert.Empty(Kernel(w.Host).QueryItems().Values.Where(item => item.Location.Kind == ItemLocationKind.World));
	}

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();

	private static void SpawnWorldItem(ItemKernelAuthority kernel, ulong id, float x, float y) =>
		Assert.True(kernel.TrySpawn(HostId, new ItemIdentity(id, "shell"), ItemLocation.World(x, y), Shell(), out _, out _));

	private static CharacterItemMsg Shell(string definitionId = "shell") => new() { ItemId = definitionId };
}
