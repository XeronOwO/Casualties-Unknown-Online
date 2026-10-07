namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The game's own content facts a cross-player limb-use chain judges with. The
/// only two questions Part A of <c>mod-cross-player-native-semantics</c> needs,
/// both answered straight out of the game's registries and never from a CUO id
/// table:
/// <list type="bullet">
/// <item><c>Item.GlobalItems</c> (Item.cs:7168) maps every item id — vanilla AND
/// mod-registered, because the item content provider writes into the same
/// dictionary — to its <c>ItemInfo</c>; a limb-drawable container is a
/// <c>LiquidItemInfo</c> (<c>LiquidItemInfo.cs:5</c>) with
/// <c>usableOnLimb</c> set (the flag <c>PlayerCamera.ApplyWoundItem</c> gates
/// <c>useLimbAction</c> on).</item>
/// <item><c>Liquids.Registry</c> (Liquids.cs:1846) maps every liquid id to its
/// <c>LiquidType</c>; <c>injectable</c> is the single flag
/// <c>WaterContainerItem.Inject</c> gates the liquid's own
/// <c>onHealthUse</c> on.</item>
/// </list>
/// The operator's own client and the host's <see cref="ILimbUseSemantics"/> seam
/// both read THIS type, so one predicate exists rather than two.
/// </summary>
internal static class GameLimbUseFacts
{
	internal static bool IsLimbUsableLiquidContainer(string? itemId)
	{
		if (string.IsNullOrEmpty(itemId) || !Item.GlobalItems.TryGetValue(itemId, out var info))
		{
			return false;
		}

		return info is LiquidItemInfo { usableOnLimb: true };
	}

	internal static bool IsInjectableLiquid(string? liquidId) =>
		!string.IsNullOrEmpty(liquidId)
		&& Liquids.Registry.TryGetValue(liquidId, out var liquid)
		&& liquid.injectable;
}
