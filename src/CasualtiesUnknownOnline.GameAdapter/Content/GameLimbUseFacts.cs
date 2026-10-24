namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The game's own content facts a cross-player limb-use chain judges with. The
/// questions both landed chains need — the injection chain (Part A of
/// <c>mod-cross-player-native-semantics</c>) and the topical chain (Part B) —
/// all answered straight out of the game's registries and never from a CUO id
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
/// <c>onHealthUse</c> on, and <c>healthUsable</c> is the single flag
/// <c>WaterContainerItem.ApplyToLimb</c> gates it on.</item>
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

	internal static bool IsHealthUsableLiquid(string? liquidId) =>
		!string.IsNullOrEmpty(liquidId)
		&& Liquids.Registry.TryGetValue(liquidId, out var liquid)
		&& liquid.healthUsable;

	/// <summary>
	/// The limb-tool family's fact: the item carries a limb action of its own and
	/// no liquid container. Native <c>PlayerCamera.ApplyWoundItem</c>
	/// (PlayerCamera.cs:739-762) is
	/// <c>body.conscious &amp;&amp; ActuallyUsableOnLimb</c> → <c>usableOnLimb</c> →
	/// <c>useLimbAction(limb, item)</c>, and for an item with no
	/// <c>WaterContainerItem</c> the <c>ActuallyUsableOnLimb</c> read
	/// (ItemInfo.cs:10-15) is <c>usableOnLimb</c> itself — which is also the flag
	/// that selects the <c>useLimbAction</c> branch over the container branch. A
	/// container is the liquid chains' and is refused here.
	/// <para>
	/// The delegate is required to be assigned as well: native would throw on the
	/// <c>useLimbAction</c> call of a <c>usableOnLimb</c> item that carries none, so
	/// this path refuses by name instead of reproducing that null dereference (the
	/// same deviation the two liquid chains record).
	/// </para>
	/// </summary>
	internal static bool IsLimbActionItem(string? itemId) =>
		!string.IsNullOrEmpty(itemId)
		&& Item.GlobalItems.TryGetValue(itemId, out var info)
		&& info is not LiquidItemInfo
		&& info.usableOnLimb
		&& info.useLimbAction is not null;

	/// <summary>
	/// The topical chain's fact: the item's own limb action IS the native topical application, or the item carries no
	/// limb action and the native dispatch falls to its container branch.
	///
	/// <para>
	/// Native <c>PlayerCamera.ApplyWoundItem</c> (<c>PlayerCamera.cs:739-763</c>) sends a <c>usableOnLimb</c> item to
	/// <c>useLimbAction(limb, item)</c> — whatever that delegate calls — and a container with the flag clear to
	/// <c>ApplyToLimb(limb, 100f)</c>. So the flag is not the answer, and neither is the item being a container: a
	/// syringe container's delegate starts an injection minigame (<c>Item.cs</c>'s saline/ringersolution/bloodbag
	/// blocks), while `waterbottle` carries no limb action at all and is applied by the native fallback.
	/// </para>
	///
	/// <para>
	/// The delegate's own compiled body answers it, read through the same <see cref="UseActionInstructions"/> reader
	/// the content classifiers use: the four vanilla limb actions that call <c>ApplyToLimb</c> are `paincream`,
	/// `woundglue`, `disinfectant` and `spraybottle` (<c>Item.cs:652</c>, <c>:676</c>, <c>:2101</c>, <c>:2125</c>), and
	/// an injector's body calls <c>Inject</c>/starts a minigame instead, which answers false. A delegate the reader
	/// cannot open answers false (fail closed: the topical chain must not claim a gesture it cannot describe).
	/// </para>
	/// </summary>
	internal static bool AppliesToLimbTopically(string? itemId)
	{
		if (string.IsNullOrEmpty(itemId) || !Item.GlobalItems.TryGetValue(itemId, out var info) || info is not LiquidItemInfo container)
		{
			return false;
		}

		if (container.useLimbAction is null)
		{
			// No limb action: native takes the container branch only when `usableOnLimb` is clear; a `usableOnLimb`
			// item without a delegate would throw natively, so it is refused here rather than reproduced.
			return !container.usableOnLimb;
		}

		var instructions = UseActionInstructions.Read(container.useLimbAction);
		return instructions is not null
			&& UseActionInstructions.Calls(instructions, typeof(WaterContainerItem), "ApplyToLimb");
	}
}
