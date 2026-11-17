using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.Persistence;

/// <summary>
/// The cut's one-placement reconcile keeps the character file's player-set presentation
/// (the item label and colour swatch) on a row the kernel holds: the kernel's checkpoint
/// carries the placement and the durability, never the label, so without this the continue
/// restores the items with an empty label and swatch 0.
/// </summary>
public class CutCharacterPlacementTests
{
	[Fact]
	public void Reconcile_KeepsTheFileNodesLabelAndColour_WhenTheKernelRowDoesNotCarryThem()
	{
		var character = new SavedCharacter(
			"5",
			new CharacterDataMsg
			{
				OwnerSteamId = 5,
				Items =
				[
					new CharacterItemMsg { InstanceId = 5, ItemId = "lantern", SlotIndex = 0, Label = "MemberColor", ColorSwatch = 5 },
				],
			},
			"Guest");
		var ownerByKey = new Dictionary<string, ulong> { ["5"] = 5 };
		var items = new List<ItemState>
		{
			new(
				new ItemIdentity(5, "lantern"),
				5,
				ItemLocation.Carried(new ActorId(5)))
			{
				Data = new ItemData(1f, false, 0, [], [], string.Empty, 0),
			},
		};

		var outcome = CutCharacterPlacement.Reconcile([character], ownerByKey, items, NullLogger.Instance);

		var restored = Assert.Single(outcome.Characters).Character.Items[0];
		Assert.Equal("MemberColor", restored.Label);
		Assert.Equal(5, restored.ColorSwatch);
	}
}
