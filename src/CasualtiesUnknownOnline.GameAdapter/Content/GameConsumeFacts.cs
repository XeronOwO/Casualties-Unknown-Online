namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The game's own content fact the cross-player consume (drink) chain judges
/// with — the mirror of <see cref="GameLimbUseFacts"/> for the family that has
/// no limb:
/// <list type="bullet">
/// <item><c>Item.GlobalItems</c> (Item.cs:7168) maps every item id — vanilla AND
/// mod-registered, because the item content provider writes into the same
/// dictionary — to its <c>ItemInfo</c>; a DRINKABLE container is a
/// <c>LiquidItemInfo</c> (<c>LiquidItemInfo.cs:5</c>) whose <c>usable</c> flag is
/// set, which is exactly the gate <c>Body.UseItem</c> applies
/// (<c>Body.cs:2475-2481</c>) before it runs the item's own <c>useAction</c>.</item>
/// </list>
/// The operator's own client and the host's <see cref="IConsumeSemantics"/> seam
/// both read THIS type, so one predicate exists rather than two.
/// </summary>
internal static class GameConsumeFacts
{
	internal static bool IsUsableLiquidContainer(string? itemId)
	{
		if (string.IsNullOrEmpty(itemId) || !Item.GlobalItems.TryGetValue(itemId, out var info))
		{
			return false;
		}

		return info is LiquidItemInfo { usable: true };
	}
}
