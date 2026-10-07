using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Applies a host-authoritative drained dose to the LOCAL body through the
/// game's own topical path. Part B of
/// <c>mod-cross-player-native-semantics</c>: the host decides that the operation
/// is allowed and owns the resource, and the AFFECTED side runs the effect, so
/// the per-ml formula and the liquid's own flags come from the game's own
/// <c>Liquids.Registry</c> entry instead of a CUO table.
/// <para>
/// The body is <c>WaterContainerItem.ApplyToLimb</c>'s per-stack loop
/// (WaterContainerItem.cs:218-234) minus its <c>CalculateDrain</c>/<c>Drain</c>
/// pair: the drain already happened on the host's authoritative item, and the
/// amounts here are its result. The <c>healthUsable</c> gate and the liquid's own
/// <c>onHealthUse</c> delegate are verbatim, which is where the timed
/// <c>CoUtils.DoTimedOp</c> bodies (reliefcream's pain ramp) start and accumulate
/// exactly as the native calls make them.
/// </para>
/// <para>
/// The call runs inside the medical capture scope on purpose: a topical
/// delegate's clip is played from inside <c>onHealthUse</c> at the limb's own
/// body position (WaterContainerItem.cs's <c>reliefcream</c> and
/// <c>woundglue</c> rows), so the patient's client is the side that owns it,
/// exactly as it would be for a local application — the operator's own client
/// plays only the item-level clip its delegate makes.
/// </para>
/// <para>
/// One deliberate deviation, the same one <see cref="NativeInjectionApply"/>
/// makes: native <c>ApplyToLimb</c> calls <c>onHealthUse</c> without a null
/// check, so a health-usable liquid that carries no delegate throws there. A mod
/// liquid can declare <c>HealthUsable</c> while the mod API has no way to give it
/// a delegate (Part 3 A of the ceiling ticket), so this path logs and skips
/// instead of taking the patient's client down with it.
/// </para>
/// </summary>
internal static class NativeTopicalApply
{
	/// <summary>
	/// Apply one drained dose to <paramref name="requestedLimbIndex"/> of the
	/// local body; returns how many stacks the game's registry answered for, so
	/// the caller knows whether the body actually moved.
	/// </summary>
	internal static int Apply(Body body, int requestedLimbIndex, IReadOnlyList<LiquidStackMsg> dose, ILogger log)
	{
		if (dose.Count == 0 || body.limbs.Length == 0)
		{
			return 0;
		}

		var limb = NativeLimbTarget.Resolve(body, requestedLimbIndex);
		if (limb is null)
		{
			log.LogWarning("[ItemUse] topical dose skipped: the local body has no usable limb for {Limb}.", requestedLimbIndex);
			return 0;
		}

		var handled = 0;
		using var scope = CallContext.Enter(CallContext.Origin.CharacterMedicalUse);
		foreach (var stack in dose)
		{
			if (stack.Amount <= 0f)
			{
				continue;
			}

			if (!Liquids.Registry.TryGetValue(stack.LiquidId, out var liquid))
			{
				log.LogWarning("[ItemUse] topical dose skipped: liquid {Liquid} is not in the game's own registry.", stack.LiquidId);
				continue;
			}

			handled++;
			if (!liquid.healthUsable)
			{
				continue;
			}

			if (liquid.onHealthUse is null)
			{
				log.LogWarning("[ItemUse] topical dose skipped: liquid {Liquid} is health-usable but carries no effect delegate — native ApplyToLimb would throw here.", stack.LiquidId);
				continue;
			}

			liquid.onHealthUse(stack.Amount, limb);
		}

		return handled;
	}
}
