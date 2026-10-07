using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Applies a host-authoritative drained dose to the LOCAL body through the
/// game's own injection path. Part A of
/// <c>mod-cross-player-native-semantics</c>: the host decides that the operation
/// is allowed and owns the resource, and the AFFECTED side runs the effect, so
/// the formula, the per-liquid flags and the timed bodies come from the game's
/// own <c>Liquids.Registry</c> entries instead of a CUO table.
/// <para>
/// The body is <c>WaterContainerItem.Inject</c>'s per-stack loop
/// (WaterContainerItem.cs:237-261) minus its <c>CalculateDrain</c>/<c>Drain</c>
/// pair: the drain already happened on the host's authoritative item, and the
/// amounts here are its result. Everything else is verbatim — the
/// <c>injectionSickness</c> sickness term, the ml-independent blood-viscosity
/// term, the <c>injectable</c> gate and the liquid's own <c>onHealthUse</c>
/// delegate, which is where the timed <c>CoUtils.DoTimedOp</c> bodies start and
/// accumulate exactly as the native per-frame calls make them.
/// </para>
/// <para>
/// One deliberate deviation: native <c>Inject</c> calls <c>onHealthUse</c>
/// without a null check, so an injectable liquid that carries no delegate throws
/// there. A mod liquid can declare <c>Injectable</c> while the mod API has no way
/// to give it a delegate (Part 3 A of the ceiling ticket), so this path logs and
/// skips instead of taking the patient's client down with it.
/// </para>
/// </summary>
internal static class NativeInjectionApply
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

		var limb = ResolveLimb(body, requestedLimbIndex);
		if (limb is null)
		{
			log.LogWarning("[MedicalOps] dose skipped: the local body has no usable limb for {Limb}.", requestedLimbIndex);
			return 0;
		}

		var handled = 0;
		foreach (var stack in dose)
		{
			if (stack.Amount <= 0f)
			{
				continue;
			}

			if (!Liquids.Registry.TryGetValue(stack.LiquidId, out var liquid))
			{
				log.LogWarning("[MedicalOps] dose skipped: liquid {Liquid} is not in the game's own registry.", stack.LiquidId);
				continue;
			}

			handled++;
			if (liquid.injectionSickness > 0f)
			{
				limb.body.sicknessAmount += liquid.injectionSickness * 0.35f * stack.Amount;
			}

			limb.body.bloodViscosity -= liquid.injectionSickness * 0.1f;
			if (!liquid.injectable)
			{
				continue;
			}

			if (liquid.onHealthUse is null)
			{
				log.LogWarning("[MedicalOps] dose skipped: liquid {Liquid} is injectable but carries no effect delegate — native Inject would throw here.", stack.LiquidId);
				continue;
			}

			liquid.onHealthUse(stack.Amount, limb);
		}

		return handled;
	}

	/// <summary>
	/// The limb the dose lands on: the operator's own pick when it is a real,
	/// attached limb of this body, otherwise the most injured one — the same
	/// automatic rule the host applies for the heal slice
	/// (<c>RemoteHealApplication.PickMostInjuredLimb</c>, pinned by
	/// <c>InjectionSemanticsTests.LimbRule_TheRequestedLimbWinsAndAnInvalidOneFallsBackToTheMostInjured</c>),
	/// evaluated on the affected side's own body.
	/// </summary>
	private static Limb? ResolveLimb(Body body, int requestedLimbIndex)
	{
		if (requestedLimbIndex >= 0
			&& requestedLimbIndex < body.limbs.Length
			&& body.limbs[requestedLimbIndex] != null // Unity object — ==
			&& !body.limbs[requestedLimbIndex].dismembered)
		{
			return body.limbs[requestedLimbIndex];
		}

		Limb? best = null;
		var bestScore = float.MaxValue;
		foreach (var candidate in body.limbs)
		{
			if (candidate == null || candidate.dismembered) // Unity object — ==
			{
				continue;
			}

			var score = candidate.skinHealth + candidate.muscleHealth;
			if (best is null || score < bestScore)
			{
				best = candidate;
				bestScore = score;
			}
		}

		return best;
	}
}
