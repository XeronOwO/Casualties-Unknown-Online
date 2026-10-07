using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The game's own content facts behind the cross-player wearable (wear) chain,
/// the third of the three content readers beside <see cref="GameLimbUseFacts"/>
/// and <see cref="GameConsumeFacts"/>:
/// <list type="bullet">
/// <item><c>Item.GlobalItems</c> (Item.cs:7168) maps every item id — vanilla AND
/// mod-registered, because the item content provider writes into the same
/// dictionary — to its <c>ItemInfo</c>; <c>wearable</c> is the flag the game's
/// own wear FLOW dispatches on (<c>Body.AutoPickUpItem</c>, Body.cs:500, and
/// <c>PlayerCamera.TryPerformRadialAction</c>, PlayerCamera.cs:1640).</item>
/// <item><c>wearSlotId</c> is what the same flow's occupancy check compares
/// (<c>Body.GetWearableBySlotID</c>, Body.cs:1590) — and the game's own save file
/// records it as the worn item's slot (SaveSystem.cs:101), which is the evidence
/// that it, not the limb, identifies the slot.</item>
/// <item><c>desiredWearLimb</c> is the limb NAME <c>Body.LimbByName</c>
/// (Body.cs:1467) resolves. The name becomes the limb INDEX the character
/// snapshot encodes as <c>-(index + 2)</c>, and the array that index refers to is
/// the character prefab's own limb order — the same order in every body of one
/// session, and the order the snapshot itself is captured in.</item>
/// </list>
/// The operator's own client and the host's <see cref="IWearSemantics"/> seam both
/// read THIS type, so one predicate exists rather than two.
/// </summary>
internal static class GameWearPlacement
{
	/// <summary>
	/// Resolve where the game wears <paramref name="itemId"/>: false when the
	/// item's own data does not make it a wearable, when the wear limb name is
	/// empty, or when <paramref name="body"/> cannot supply the limb layout (no
	/// body in this scene yet).
	/// </summary>
	internal static bool TryResolve(Body? body, string? itemId, out int limbIndex, out string wearSlotId)
	{
		limbIndex = -1;
		wearSlotId = "";
		if (body == null // Unity object — ==
			|| string.IsNullOrEmpty(itemId)
			|| !Item.GlobalItems.TryGetValue(itemId, out var info)
			|| info is not { wearable: true }
			|| string.IsNullOrEmpty(info.desiredWearLimb))
		{
			return false;
		}

		var limbs = body.limbs;
		if (limbs is null)
		{
			return false;
		}

		for (var i = 0; i < limbs.Length; i++)
		{
			if (limbs[i] != null && limbs[i].name == info.desiredWearLimb) // Unity object — ==
			{
				limbIndex = i;
				wearSlotId = info.wearSlotId;
				return true;
			}
		}

		return false;
	}
}
