namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cross-player LIMB-TOOL family's one admission rule, split out of
/// <see cref="PlayerItemUseService"/> at the architecture line gate so the
/// operator's gesture gate and the host's chain ask the same question.
/// <para>
/// One native predicate answers it — the item's own limb action, no liquid
/// container (<see cref="ILimbUseSemantics.IsLimbActionItem"/>) — and the
/// exclusions below are the claims of the families that have NOT migrated yet.
/// Each names the chain that owns the item today and therefore what lifts it:
/// </para>
/// <list type="bullet">
/// <item><see cref="RemoteHealProfiles"/> — the heal item set (bandage family plus
/// the one-shot <c>adhesivebandage</c>) and the minigame session behind it: the
/// next stage of <c>mod-cross-player-native-semantics</c>'s limb-tool chain;</item>
/// <item><see cref="RemoteBandageMinigameCatalog"/> — the wound-view bandage
/// minigame session, whose gesture is the OPERATOR's and whose effect is applied
/// by the host from a transcribed profile;</item>
/// <item><see cref="RemoteOtherMedicalCatalog"/> — the amputation, defibrillator
/// and dislocation-wrench sessions on the medical-operation wire;</item>
/// <item><see cref="ShrapnelStartValidator"/> — the shared shrapnel session's
/// tweezers;</item>
/// <item>the SOLID-FOOD family — and this one is asked through the item's own data
/// like this family's own predicate, because an item can be both: <c>bulbskin</c>
/// drinks 4.5 in its <c>useAction</c> (<c>Item.cs:2534-2544</c>) and
/// <c>xalorissponge</c> eats 8 (<c>Item.cs:2571-2578</c>), while both also carry a
/// limb action. The host's chain asks the solid-food rule BEFORE this one, so
/// without this line a wound-view gesture on such an item would make the treated
/// player EAT it — the gesture carries no statement of which action was meant, and
/// the family order is what decides. Their limb half stays unreachable cross-player
/// until the request can say which gesture it was; that gap is recorded on the
/// ticket rather than hidden here.</item>
/// </list>
/// <para>
/// So an item reaches this family only when no other chain claims it, and every
/// claim is a session whose gesture is a minigame, an operation of its own, or the
/// body-feeding action. When one of those chains migrates, its line leaves this rule
/// and the item joins the native run — which is the only order that keeps one
/// gesture from being routed twice.
/// </para>
/// </summary>
public static class LimbToolAdmission
{
	/// <summary>
	/// True when the item's own data makes it this family's and no other chain claims
	/// it. <paramref name="itemId"/> may be null/empty; the answer is false.
	/// </summary>
	public static bool IsLimbTool(ILimbUseSemantics limbSemantics, ISolidFoodSemantics solidFoodSemantics, string itemId)
	{
		if (!limbSemantics.IsLimbActionItem(itemId))
		{
			return false;
		}

		if (SolidFoodAdmission.Classify(solidFoodSemantics, itemId) != SolidFoodVerdict.NotSolidFood)
		{
			return false;
		}

		if (RemoteHealProfiles.IsHealItem(itemId)
			|| RemoteBandageMinigameCatalog.IsBandageItem(itemId)
			|| RemoteOtherMedicalCatalog.IsAed(itemId)
			|| RemoteOtherMedicalCatalog.IsManualDefibrillator(itemId)
			|| RemoteOtherMedicalCatalog.IsAmputationTool(itemId)
			|| RemoteOtherMedicalCatalog.IsDislocationWrench(itemId)
			|| ShrapnelStartValidator.IsTweezers(itemId))
		{
			return false;
		}

		return true;
	}
}
