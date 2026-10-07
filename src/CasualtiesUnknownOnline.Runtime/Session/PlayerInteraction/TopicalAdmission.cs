using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The topical chain's admission rule, in one place so the host's one-shot
/// request check and the operator's own client cannot drift apart: the item's
/// own data says it is a liquid container a limb action may draw from, AND at
/// least one of its stacks holds a health-usable liquid.
/// <para>
/// The mirror image of <see cref="InjectionAdmission"/>, and for the same
/// reason: native <c>PlayerCamera.ApplyWoundItem</c> sends both families through
/// <c>useLimbAction</c>, and only the liquid's own <c>LiquidType</c> flags say
/// which native call the item's delegate makes — <c>injectable</c> reaches
/// <c>WaterContainerItem.Inject</c>, <c>healthUsable</c> reaches
/// <c>WaterContainerItem.ApplyToLimb</c>. Whichever chain's flag matches first
/// owns the gesture, which is why the two checks are ordered injection-first at
/// every routing site: vanilla carries no liquid with both flags, and a mod one
/// would be injected, the branch that actually applies a per-ml effect there.
/// </para>
/// <para>
/// This replaces the deleted <c>RemoteTopicalCatalog</c> id table, so a mod
/// topical container and a vanilla item the table never carried both qualify,
/// and the per-item ml the table transcribed is no longer needed here: the dose
/// is the one the item's own delegate computes on the operator's client.
/// </para>
/// </summary>
public static class TopicalAdmission
{
	/// <summary>True when this item, with these stacks, is the topical chain's business.</summary>
	public static bool IsTopicalContainer(
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
			if (semantics.IsHealthUsableLiquid(liquid.LiquidId))
			{
				return true;
			}
		}

		return false;
	}
}
