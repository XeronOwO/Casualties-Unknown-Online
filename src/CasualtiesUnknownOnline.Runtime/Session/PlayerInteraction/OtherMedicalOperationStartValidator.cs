using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The OPERATOR half of a Stage 3 medical operation start: which item the operator
/// is asking with. It is the host's question to answer, because the item facts it
/// reads are the host's own item authority (the same authority the kernel commit
/// gates on) rather than a judgment about the target's body. The target's body half
/// lives in <see cref="MedicalTargetBodyValidator"/> and is answered by the target's
/// own client; the two halves meet in the host's parked start request.
/// </summary>
internal static class OtherMedicalOperationStartValidator
{
	internal static bool TryValidate(
		CharacterDataMsg operatorData,
		MedicalOperationStartRequestMsg msg,
		out string reason)
	{
		reason = "";
		var itemId = msg.ItemInstanceId == 0 ? "" : FindItemId(operatorData, msg.ItemInstanceId);
		switch (msg.Kind)
		{
			case MedicalOperationKind.Bandage:
				reason = RemoteBandageMinigameCatalog.IsBandageItem(itemId)
					? ""
					: "Item is not a bandage/dressing minigame item.";
				return reason.Length == 0;
			case MedicalOperationKind.SplintRemoval:
			case MedicalOperationKind.TourniquetRemoval:
				return true;
			case MedicalOperationKind.Dislocation:
				// A bare hand may relocate a dislocation; a wrench only has to be a wrench when one was named.
				reason = msg.ItemInstanceId == 0 || RemoteOtherMedicalCatalog.IsDislocationWrench(itemId)
					? ""
					: "Item is not a dislocation wrench.";
				return reason.Length == 0;
			case MedicalOperationKind.Aed:
				reason = itemId == "aed" ? "" : "Item is not an AED.";
				return reason.Length == 0;
			case MedicalOperationKind.ManualDefib:
				reason = itemId == "manualdefibrillator" ? "" : "Item is not a manual defibrillator.";
				return reason.Length == 0;
			case MedicalOperationKind.Amputation:
				reason = RemoteOtherMedicalCatalog.IsAmputationTool(itemId) ? "" : "Item is not an amputation tool.";
				return reason.Length == 0;
			default:
				reason = "Unsupported operation.";
				return false;
		}
	}

	private static string FindItemId(CharacterDataMsg data, ulong itemInstanceId)
	{
		for (var i = 0; i < data.Items.Count; i++)
		{
			if (data.Items[i].InstanceId == itemInstanceId)
			{
				return data.Items[i].ItemId;
			}
		}

		return "";
	}
}
