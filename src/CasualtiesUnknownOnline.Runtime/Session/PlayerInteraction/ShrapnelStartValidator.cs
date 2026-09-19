using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure start-request validation for the shrapnel family: the operator's own
/// facts (conscious, alive, holding usable tweezers) and the target limb's facts
/// (exists, not dismembered, still carries shrapnel), read from the two character
/// snapshots. It touches no session state — the reservations and the piece layout
/// stay the service's business — so the family's rules are unit-testable without a
/// session and the refusal reasons keep one home.
/// </summary>
internal static class ShrapnelStartValidator
{
	/// <summary>
	/// Answers whether the pair of snapshots allows this start, and reports the
	/// target limb's own shrapnel count so the session's piece layout is built from
	/// the count the validation actually accepted.
	/// </summary>
	internal static bool TryValidate(
		ulong operatorId,
		CharacterDataMsg? operatorData,
		CharacterDataMsg? targetData,
		MedicalOperationStartRequestMsg msg,
		MedicalOperationClaims claims,
		out int shrapnelCount,
		out string reason)
	{
		reason = "";
		shrapnelCount = 0;

		if (operatorData?.Health is not { } operatorHealth || !operatorHealth.Conscious || !operatorHealth.Alive)
		{
			reason = "Operator is not conscious/alive.";
			return false;
		}

		if (targetData?.Health is not { } targetHealth || !targetHealth.Conscious || !targetHealth.Alive)
		{
			reason = "Target is not conscious/alive.";
			return false;
		}

		if (msg.LimbIndex < 0 || msg.LimbIndex >= targetData.Limbs.Count)
		{
			reason = "Target limb not found.";
			return false;
		}

		var targetLimb = targetData.Limbs[msg.LimbIndex];
		if (targetLimb.Dismembered || targetLimb.Shrapnel <= 0)
		{
			reason = "Target limb has no shrapnel.";
			return false;
		}

		if (claims.IsOperatorBusy(operatorId))
		{
			reason = "Operator already has an active medical operation.";
			return false;
		}

		if (msg.ItemInstanceId != 0)
		{
			var itemIndex = PlayerItemIndex.Find(operatorData, msg.ItemInstanceId);
			if (itemIndex < 0 || itemIndex >= operatorData.Items.Count)
			{
				reason = "Tweezers not found.";
				return false;
			}

			var item = operatorData.Items[itemIndex];
			if (item.ItemId != "tweezers" || item.Condition <= 0f)
			{
				reason = "Item is not usable tweezers.";
				return false;
			}

			if (claims.IsItemReserved(msg.ItemInstanceId))
			{
				reason = "Item is already reserved.";
				return false;
			}
		}

		shrapnelCount = targetLimb.Shrapnel;
		return true;
	}
}
