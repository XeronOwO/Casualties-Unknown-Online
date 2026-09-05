using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Pure owner-side mouth-sprite rule, mirroring the game's own
/// <c>FacialExpression.Update</c> head-sprite branch:
/// <c>defaultHeadMouth</c> when <c>eatTime &gt; 0.15</c>, the player is holding
/// the mouth slot item, or the head limb is dislocated;
/// <c>defaultHeadMouthHalf</c> when only a short <c>eatTime</c> remains;
/// <c>defaultHead</c> otherwise. Disfigured bodies use the dedicated
/// disfigurement sprites, so they report closed and let the clone's synced
/// disfigurement path keep the visual authority.
/// </summary>
internal static class HeadMouthRule
{
	internal static HeadMouthState Evaluate(
		bool disfigured,
		float eatTime,
		bool holdingMouthItem,
		bool headDislocated)
	{
		if (disfigured)
		{
			return HeadMouthState.Closed;
		}

		if (eatTime > 0.15f || holdingMouthItem || headDislocated)
		{
			return HeadMouthState.Open;
		}

		return eatTime > 0f ? HeadMouthState.HalfOpen : HeadMouthState.Closed;
	}

	/// <summary>
	/// Recompute the head/mouth state on the receiving side after a fact-table
	/// event changed the owner's slot layout or limb latches without a fresh
	/// 1 Hz snapshot. Keeps the replayed mouth state from being pinned to the
	/// last snapshot while the clone's own slot/limb data is already current.
	/// </summary>
	internal static void Refresh(CharacterDataMsg data)
	{
		var health = data.Health;
		if (health is null)
		{
			return;
		}

		var holdingMouthItem = false;
		foreach (var item in data.Items)
		{
			if (item.SlotIndex == 2)
			{
				holdingMouthItem = true;
				break;
			}
		}

		var headDislocated = false;
		foreach (var limb in data.Limbs)
		{
			if (limb.Index == 0)
			{
				headDislocated = limb.Dislocated;
				break;
			}
		}

		health.HeadMouth = Evaluate(
			health.Disfigured,
			health.EatTime,
			holdingMouthItem,
			headDislocated);
	}
}
