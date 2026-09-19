using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The one home for the re-check the three medical families run when the target's
/// own verdict arrives. A verdict is a round trip old, and the checks it skipped
/// (a participant who left, an item or limb another operation claimed in the
/// meantime) are the host's own facts — the same facts, with the same refusal
/// wording, in every family. Keeping one copy is what makes the three families
/// answer a raced start identically instead of three ways.
/// </summary>
internal static class MedicalStartRecheck
{
	internal static MedicalStartRecheckOutcome Run(
		PlayerCharacterAccess access,
		MedicalOperationClaims claims,
		ulong operatorId,
		ulong target,
		MedicalOperationStartRequestMsg msg,
		bool limbClaimApplies,
		out string reason)
	{
		reason = "";
		if (!access.IsInWorld(operatorId))
		{
			return MedicalStartRecheckOutcome.OperatorGone;
		}

		if (!access.IsInWorld(target))
		{
			reason = "Participants are not in-world.";
			return MedicalStartRecheckOutcome.Reject;
		}

		if (claims.IsOperatorBusy(operatorId))
		{
			reason = "Operator already has an active medical operation.";
			return MedicalStartRecheckOutcome.Reject;
		}

		if (msg.ItemInstanceId != 0 && claims.IsItemReserved(msg.ItemInstanceId))
		{
			reason = "Item is already reserved.";
			return MedicalStartRecheckOutcome.Reject;
		}

		// A shared shrapnel session already HOLDS the limb, so a second operator
		// joining it must not be refused by the claim the session itself made; the
		// caller passes false for that path and true for a start that would create one.
		if (limbClaimApplies && msg.LimbIndex >= 0 && claims.IsLimbReserved(target, msg.LimbIndex))
		{
			reason = "Target limb is already reserved.";
			return MedicalStartRecheckOutcome.Reject;
		}

		return MedicalStartRecheckOutcome.Proceed;
	}
}
