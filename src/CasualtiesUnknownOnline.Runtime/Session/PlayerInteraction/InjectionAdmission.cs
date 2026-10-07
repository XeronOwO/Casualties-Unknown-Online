using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The injection chain's admission rule, in one place so the host's start check
/// and the operator's own client cannot drift apart: the item's own data says it
/// is a liquid container a limb action may draw from, AND at least one of its
/// stacks holds an injectable liquid.
/// <para>
/// The second half is what separates the syringe family from the topical family:
/// native <c>PlayerCamera.ApplyWoundItem</c> sends both through
/// <c>useLimbAction</c>, and only the liquid's own
/// <c>LiquidType.injectable</c> / <c>healthUsable</c> flags say which native call
/// the delegate makes. A mixed container passes — native
/// <c>WaterContainerItem.Inject</c> draws every stack and applies an effect only
/// where <c>injectable</c> is set, so an inert carrier is the game's own case,
/// not a refusal.
/// </para>
/// </summary>
public static class InjectionAdmission
{
	/// <summary>True when this item, with these stacks, is the injection chain's business.</summary>
	public static bool IsInjectableContainer(
		ILimbUseSemantics semantics,
		string itemId,
		IReadOnlyList<LiquidStackMsg>? liquids)
	{
		if (liquids is null || liquids.Count == 0 || !semantics.IsLimbUsableLiquidContainer(itemId))
		{
			return false;
		}

		foreach (var liquid in liquids)
		{
			if (semantics.IsInjectableLiquid(liquid.LiquidId))
			{
				return true;
			}
		}

		return false;
	}
}
