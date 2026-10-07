using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The data anchor of the standing-item-object category (ticket
/// <c>mod-cross-player-solid-food-semantics</c>): the Game Adapter's new item category rests on ONE
/// data fact — whether the authoritative item data holds an id as a WORLD row — and the item surface
/// hands that fact over as <c>IItemControl.IsWorldItemRegistered</c>. This pins the reading the
/// category needs: a world row answers true, a carried row answers false, an unbound id answers
/// false, and the answer follows the DATA when one item moves between the two (the moment a standing
/// object stops being one).
/// </summary>
[Trait("Category", "Integration")]
public class ItemWorldRowQueryTests
{
	private static CharacterItemMsg Item(ulong instanceId, string definitionId = "bandage") => new()
	{
		InstanceId = instanceId,
		ItemId = definitionId,
		Condition = 1f,
		Contents = [],
	};

	[Fact]
	public void TheItemSurface_AnswersWorldRowsAndOnlyWorldRows()
	{
		using var w = ItemSimWorld.Create();
		var hostItems = w.Host.Services.GetRequiredService<IItemControl>();
		var guestItems = w.G1.Services.GetRequiredService<IItemControl>();

		// An unbound id is never a world row — the category's answer for an object whose id no data
		// carries yet.
		Assert.False(hostItems.IsWorldItemRegistered(0), "id 0 is never a world row");

		// The guest reports an item it generated: the host arbitrates it into the world table.
		w.Spawn(w.G1, 501, Item(501));
		w.Driver.Tick(50);
		Assert.True(hostItems.IsWorldItemRegistered(501), "a spawned world item is a world row on the host");

		// The same item is picked up: the row leaves the world. The category is decided by the data,
		// so the answer follows the move rather than the object.
		w.Pickup(w.G1, 501, Item(501));
		w.Driver.Tick(50);
		Assert.False(hostItems.IsWorldItemRegistered(501), "a picked-up item is no longer a world row");

		// A guest's own carried inventory is a CARRIED row: the host records it in the transfer table,
		// which is not the world table — on the owner's side it is not a world row either.
		guestItems.SendCarriedInventory([Item(601)]);
		w.Driver.Tick(50);
		Assert.True(w.TransferredOf(w.G1, 601), "the carried row must have reached the host's transfer table");
		Assert.False(hostItems.IsWorldItemRegistered(601), "a carried row is not a world row");
		Assert.False(guestItems.IsWorldItemRegistered(601), "the owner's own carried item is not a world row either");
	}
}
