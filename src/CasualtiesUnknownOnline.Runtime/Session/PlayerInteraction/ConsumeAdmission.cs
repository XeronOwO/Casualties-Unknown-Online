using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The consume chain's admission rule, in one place so the host's one-shot
/// request check and the operator's own client cannot drift apart: the item's
/// own data says the game may USE it as a liquid container
/// (<see cref="IConsumeSemantics.IsUsableLiquidContainer"/> — the flag
/// <c>Body.UseItem</c> gates the item's <c>useAction</c> on), AND it holds at
/// least one liquid stack for the drink to draw from.
/// <para>
/// Nothing here reads the LIQUID: native <c>WaterContainerItem.Drink</c> has no
/// liquid-level gate at all — it drains whatever the container holds and calls
/// each liquid's own <c>onDrink</c>, so an unknown or effect-less liquid is the
/// game's own case rather than a reason to refuse the gesture.
/// </para>
/// <para>
/// This replaces the deleted <c>RemoteConsumeCatalog</c> liquid table and
/// <c>RemoteDrinkMedicineCatalog</c>, which carried the drinkable item ids with
/// a per-item ml amount and a per-liquid effect coefficient table. Neither is
/// needed any more: the dose is the ml the item's own delegate computes on the
/// operator's client, and the effect is the liquid's own <c>onDrink</c>
/// delegate, executed on the affected side.
/// </para>
/// </summary>
public static class ConsumeAdmission
{
	/// <summary>True when this item, with these stacks, is the consume chain's business.</summary>
	public static bool IsDrinkContainer(
		IConsumeSemantics semantics,
		string itemId,
		IReadOnlyList<LiquidStackMsg>? liquids) =>
		liquids is { Count: > 0 } && semantics.IsUsableLiquidContainer(itemId);
}
