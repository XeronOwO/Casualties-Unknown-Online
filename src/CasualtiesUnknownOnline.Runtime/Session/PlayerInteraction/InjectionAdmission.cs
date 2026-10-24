using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The injection chain's admission rule, in one place so the host's start check
/// and the operator's own client cannot drift apart: the item's own data says it
/// is a liquid container a limb action may draw from, AND the gesture is the injection shape.
/// <para>
/// The second half is what separates the syringe family from the topical family:
/// native <c>PlayerCamera.ApplyWoundItem</c> sends both through
/// <c>useLimbAction</c>, and the item's own action says which native call the delegate makes. A stack the game marks
/// injectable admits the container outright; native <c>WaterContainerItem.Inject</c> draws every stack and applies an
/// effect only where <c>injectable</c> is set, so an inert carrier is the game's own case, not a refusal.
/// </para>
/// <para>
/// A container with NO injectable stack is admitted by the SHAPE of its own limb action rather than by its liquid:
/// a syringe's delegate is the injection flow, and the native wound view runs it whatever the player filled the
/// syringe with (<c>PlayerCamera.ApplyWoundItem</c> tests <c>ActuallyUsableOnLimb</c>, not the liquid's flags). Refusing
/// that combination because our own classification had no branch was the defect ticket
/// <c>topical-limb-action-shape.md</c> recorded; native parity is the answer.
/// </para>
/// <para>
/// What that does NOT do is widen the topical family: <see cref="ILimbUseSemantics.AppliesToLimbTopically"/> answers
/// true for the apply-to-limb delegate and for a container with no limb action at all (the native fallback), so a
/// water bottle stays a pour and never becomes an injection, and a syringe never becomes a pour.
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

		// No injectable stack. The stack rule is the whole admission: the SHAPE of the container's own limb action is
		// asked separately, and only where a limb gesture is what is being classified (the wound view's release and the
		// limb entry), because a container with no injectable liquid is still a DRINK when the inventory entry uses it.
		return false;
	}

	/// <summary>
	/// True when the container's own limb action is the injection flow whatever it holds: a limb-drawable liquid
	/// container whose native limb action is NOT the topical application. Native
	/// <c>PlayerCamera.ApplyWoundItem</c> runs <c>useLimbAction</c> for every <c>ActuallyUsableOnLimb</c> item, and a
	/// syringe's delegate starts the syringe flow even when the player filled it with a health-usable liquid — the game
	/// allows that fill, and refusing it because our classification had no branch was the defect ticket
	/// `topical-limb-action-shape.md` recorded.
	/// <para>
	/// Asked only by the LIMB-shaped entries (the wound view's release and the limb entry of the family classifier): the
	/// inventory use never runs a limb action, so a drinkable bag stays the inventory drink it always was.
	/// </para>
	/// </summary>
	public static bool IsInjectionShapedLimbContainer(ILimbUseSemantics semantics, string itemId) =>
		semantics.IsLimbUsableLiquidContainer(itemId) && !semantics.AppliesToLimbTopically(itemId);
}
