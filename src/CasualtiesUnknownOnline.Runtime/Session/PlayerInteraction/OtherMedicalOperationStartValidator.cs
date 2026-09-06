using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure semantic validation for Stage 3 medical operation starts. It checks
/// the target limb/component state and the operator's item id without touching
/// session state or reservations.
/// </summary>
internal static class OtherMedicalOperationStartValidator
{
	internal static bool TryValidate(
		CharacterDataMsg targetData,
		CharacterDataMsg operatorData,
		MedicalOperationStartRequestMsg msg,
		out string reason)
	{
		reason = "";
		if (msg.LimbIndex < 0 || msg.LimbIndex >= targetData.Limbs.Count)
		{
			reason = "Target limb not found.";
			return false;
		}

		var limb = targetData.Limbs[msg.LimbIndex];
		var itemId = msg.ItemInstanceId == 0 ? "" : FindItemId(operatorData, msg.ItemInstanceId);
		switch (msg.Kind)
		{
			case MedicalOperationKind.Bandage:
				if (limb.Dismembered)
				{
					reason = "Target limb is dismembered.";
					return false;
				}

				if (!RemoteBandageMinigameCatalog.IsBandageItem(itemId))
				{
					reason = "Item is not a bandage/dressing minigame item.";
					return false;
				}

				return true;
			case MedicalOperationKind.SplintRemoval:
				reason = limb.Components.Any(c => c.TypeName == "SplintLimb")
					? ""
					: "Target limb has no splint.";
				return reason.Length == 0;
			case MedicalOperationKind.TourniquetRemoval:
				reason = limb.Components.Any(c => c.TypeName == "TourniquetScript")
					? ""
					: "Target limb has no tourniquet.";
				return reason.Length == 0;
			case MedicalOperationKind.Dislocation:
				if (!limb.Dislocated || limb.Dismembered)
				{
					reason = "Target limb is not dislocated.";
					return false;
				}

				if (msg.ItemInstanceId != 0 && !RemoteOtherMedicalCatalog.IsDislocationWrench(itemId))
				{
					reason = "Item is not a dislocation wrench.";
					return false;
				}

				return true;
			case MedicalOperationKind.Aed:
				reason = itemId == "aed" ? "" : "Item is not an AED.";
				return reason.Length == 0;
			case MedicalOperationKind.ManualDefib:
				reason = itemId == "manualdefibrillator" ? "" : "Item is not a manual defibrillator.";
				return reason.Length == 0;
			case MedicalOperationKind.Amputation:
				if (limb.Dismembered || limb.IsHead || limb.IsVital)
				{
					reason = "Target limb cannot be amputated.";
					return false;
				}

				if (limb.InfectionAmount <= 60f)
				{
					reason = "Target limb is not infected enough for amputation.";
					return false;
				}

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
