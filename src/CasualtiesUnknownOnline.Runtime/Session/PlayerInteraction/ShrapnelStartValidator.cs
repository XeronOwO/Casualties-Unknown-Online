using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The OPERATOR half of a shrapnel-session start: the operator's own body is up
/// (a minigame needs an operator who is conscious), the operator holds no other
/// operation of the family, and the tweezers it named are usable and unclaimed.
/// The TARGET limb's facts — that the limb exists, is not dismembered and still
/// carries fragments, plus the live piece count the session's layout is built
/// from — are the target's own, answered by the target's client through
/// <see cref="MedicalTargetBodyValidator"/>.
/// </summary>
internal static class ShrapnelStartValidator
{
	internal static bool TryValidateOperator(
		ulong operatorId,
		CharacterDataMsg? operatorData,
		MedicalOperationStartRequestMsg msg,
		MedicalOperationClaims claims,
		out string reason)
	{
		reason = "";

		if (operatorData?.Health is not { } operatorHealth || !operatorHealth.Conscious || !operatorHealth.Alive)
		{
			reason = "Operator is not conscious/alive.";
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

		return true;
	}
}
