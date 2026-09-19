using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The one home for the re-check the three medical families run when the target's
/// own verdict arrives. A verdict is a round trip old, and the checks it skipped
/// (a participant who left, an item another operation claimed in the meantime)
/// are the host's own facts — the same facts, with the same refusal wording, in
/// every family. Keeping one copy is what makes the three families answer a raced
/// start identically instead of three ways.
/// </summary>
/// <remarks>
/// The target limb is deliberately not part of this re-check: several operators
/// may work one limb at the same time, so a limb claim a start might have raced
/// is not a fact that can refuse it. The unit that resolves once is arbitrated
/// when a completion lands (see <see cref="MedicalOperationUnitRules"/>), which is
/// also where the losing operator gets its answer.
/// </remarks>
internal static class MedicalStartRecheck
{
	internal static MedicalStartRecheckOutcome Run(
		PlayerCharacterAccess access,
		MedicalOperationClaims claims,
		ulong operatorId,
		ulong target,
		MedicalOperationStartRequestMsg msg,
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

		return MedicalStartRecheckOutcome.Proceed;
	}
}
