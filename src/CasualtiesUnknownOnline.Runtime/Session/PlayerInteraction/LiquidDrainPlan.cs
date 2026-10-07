using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The drain a cross-player limb action commits from a liquid container: the
/// requested amount capped at the container's own total, split across every
/// stack in proportion to what it holds. It mirrors
/// <c>WaterContainerItem.CalculateDrain</c> — the game's own draw for one
/// <c>Drink</c> / <c>ApplyToLimb</c> / <c>Inject</c> call — and holds no
/// per-content constant: the amount comes from the caller (the native delegate's
/// own dose on the operator's client, or the host's reconciliation of a
/// reported total).
/// </summary>
public static class LiquidDrainPlan
{
	/// <summary>
	/// Build the proportional draw of <paramref name="amount"/> ml. False when
	/// there is nothing to draw (no stacks, no liquid, or an amount that is not a
	/// positive finite number), which is the native path's own early return. The
	/// finiteness check is not decoration: the amount is a client-reported dose, and
	/// a NaN would propagate through <c>Math.Min</c> into every stack's amount and
	/// poison the item's liquid state.
	/// </summary>
	public static bool TryCreate(
		IReadOnlyList<LiquidStackMsg>? liquids,
		float amount,
		out List<LiquidStackMsg> drained)
	{
		drained = [];
		if (liquids is null || liquids.Count == 0 || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
		{
			return false;
		}

		var total = 0f;
		foreach (var liquid in liquids)
		{
			total += liquid.Amount;
		}

		if (total <= 0f)
		{
			return false;
		}

		var draw = Math.Min(amount, total);
		foreach (var liquid in liquids)
		{
			drained.Add(new LiquidStackMsg
			{
				LiquidId = liquid.LiquidId,
				Amount = liquid.Amount * (draw / total),
			});
		}

		return true;
	}
}
