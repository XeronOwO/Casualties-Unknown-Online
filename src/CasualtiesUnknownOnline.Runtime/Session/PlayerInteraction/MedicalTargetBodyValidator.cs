using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The TARGET half of a medical-operation start: everything the operation needs
/// to know about the target's OWN body — whether it is still there, whether it
/// is a body this action can act on at all, and (for the shrapnel family) the
/// limb's live piece count. It is a pure function of one character snapshot, so
/// the client that owns that body can run it and answer with a fact it observed
/// rather than the host inferring one from a report that is stale by a latency.
/// The operator's own facts are NOT here: the item half stays with the host's
/// item authority, and the operator's own body facts stay with the participant
/// gate (see the ticket's "recorded with a reason" list).
/// </summary>
internal static class MedicalTargetBodyValidator
{
	/// <param name="shrapnelCount">The target limb's live piece count on an accepted fragment pull — the session's piece layout is built from the count this validation actually accepted.</param>
	internal static bool TryValidate(
		CharacterDataMsg targetData,
		MedicalOperationKind kind,
		int limbIndex,
		out int shrapnelCount,
		out string reason)
	{
		reason = "";
		shrapnelCount = 0;

		// A responsive patient is per family, because the rule is the family's: an
		// injection and a fragment pull need one (both run against the operator's aim
		// on a body that reacts), while bandaging, splint/ tourniquet removal,
		// relocation, defibrillation and amputation are exactly what an unresponsive
		// patient needs. The distinction is preserved from the per-family checks this
		// validator replaced, not invented here.
		var responsiveTargetRequired = kind is MedicalOperationKind.Injection or MedicalOperationKind.Shrapnel;
		if (targetData.Health is not { } health || !health.Alive || (responsiveTargetRequired && !health.Conscious))
		{
			reason = responsiveTargetRequired ? "Target is not conscious/alive." : "Target is not alive.";
			return false;
		}

		// An injection is not a limb-scoped action (its request may carry no limb at
		// all), so the limb gate belongs to the families that need one — the same
		// split the per-family validators had.
		var limbScoped = kind != MedicalOperationKind.Injection;
		if (limbScoped && (limbIndex < 0 || limbIndex >= targetData.Limbs.Count))
		{
			reason = "Target limb not found.";
			return false;
		}

		if (!limbScoped)
		{
			return true;
		}

		var limb = targetData.Limbs[limbIndex];
		switch (kind)
		{
			case MedicalOperationKind.Aed:
			case MedicalOperationKind.ManualDefib:
				return true;
			case MedicalOperationKind.Shrapnel:
				if (limb.Dismembered || limb.Shrapnel <= 0)
				{
					reason = "Target limb has no shrapnel.";
					return false;
				}

				shrapnelCount = limb.Shrapnel;
				return true;
			case MedicalOperationKind.Bandage:
				reason = limb.Dismembered ? "Target limb is dismembered." : "";
				return reason.Length == 0;
			case MedicalOperationKind.SplintRemoval:
				reason = limb.Components.Any(c => c.TypeName == "SplintLimb") ? "" : "Target limb has no splint.";
				return reason.Length == 0;
			case MedicalOperationKind.TourniquetRemoval:
				reason = limb.Components.Any(c => c.TypeName == "TourniquetScript") ? "" : "Target limb has no tourniquet.";
				return reason.Length == 0;
			case MedicalOperationKind.Dislocation:
				reason = limb.Dislocated && !limb.Dismembered ? "" : "Target limb is not dislocated.";
				return reason.Length == 0;
			case MedicalOperationKind.Amputation:
				if (limb.Dismembered || limb.IsHead || limb.IsVital)
				{
					reason = "Target limb cannot be amputated.";
					return false;
				}

				reason = limb.InfectionAmount > 60f ? "" : "Target limb is not infected enough for amputation.";
				return reason.Length == 0;
			default:
				reason = "Unsupported operation.";
				return false;
		}
	}
}
