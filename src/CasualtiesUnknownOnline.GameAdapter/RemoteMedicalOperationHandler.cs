using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Remote-medical WoundView gesture routing. It maps a local dragged medical
/// item released on a remote body-limb diagram to the host-authoritative
/// medical operation session and keeps the display-only remote body copy
/// untouched. Injectable/IV medicines run the item's OWN native
/// <c>useLimbAction</c> (the game picks its syringe minigame or its one-shot
/// injection, and computes the ml), the resulting native
/// <c>WaterContainerItem.Inject</c> call is diverted into the session's dose
/// stream, and the host remains the only authority for the committed
/// item/target state (see <c>mod-cross-player-native-semantics</c>, Part A).
/// </summary>
internal sealed class RemoteMedicalOperationHandler
{
	private readonly RemoteInjectionUseHandler _injectionOps;

	private readonly GameAdapterDomains _domains;
	private readonly RemoteShrapnelOperationHandler _shrapnelOps;
	private readonly RemoteOtherMedicalOperationHandler _otherOps;

	internal RemoteMedicalOperationHandler(GameAdapterDomains domains)
	{
		_domains = domains;
		_shrapnelOps = new RemoteShrapnelOperationHandler(domains);
		_otherOps = new RemoteOtherMedicalOperationHandler(domains);
		_injectionOps = new RemoteInjectionUseHandler(domains);
	}

	internal bool TryHandleLimbUse(Item dragItem, int limbIndex)
	{
		if (!RemoteMedicalView.IsOpen || dragItem == null) // Unity object — ==
		{
			return false;
		}

		// A remote-backpack display proxy belongs to another player's
		// inventory; in the medical view the acting player can only use their
		// own carried item on the remote subject.
		if (dragItem.GetComponent<RemoteCloneRender>() != null) // Unity object — ==
		{
			_domains.Log.LogWarning("[MedicalView] refused limb use: dragged item {ItemId} is a remote display proxy, not a local medical item.",
				dragItem.id);
			return false;
		}

		var target = RemoteMedicalView.TargetSteamId;
		if (target == 0)
		{
			_domains.Log.LogWarning("[MedicalView] refused limb use: remote medical focus has no target.");
			return false;
		}

		var instance = dragItem.GetComponent<ItemInstanceId>();
		if (instance == null || instance.Id == 0) // Unity object — ==
		{
			_domains.Log.LogWarning("[MedicalView] refused limb use: local item {ItemId} has no authoritative instance id.",
				dragItem.id);
			return false;
		}

		if (!LocalUseItemEligibility.IsMedicalLimbUseItem(dragItem, _domains.LimbUseSemantics))
		{
			_domains.Log.LogWarning("[MedicalView] refused limb use: {ItemId} is not a supported remote medical/limb-treatment item.",
				dragItem.id);
			return false;
		}

		var dispatch = TryDispatchLimbUse(dragItem, limbIndex, target, instance.Id);
		if (dispatch == LimbUseDispatch.Refused)
		{
			return false;
		}

		// The native limb action — the call that would have played the item's own
		// clip at the treated limb — is blocked in this view, so the treatment was
		// silent on every side. Play what it would have played, inside the scope
		// the character-sound capture reads: the operator hears it here and every
		// other member receives it through the existing relay. An injection
		// dispatch already RAN the item's own native limb action, whose clip was
		// captured and relayed inside that window; replaying the table's copy
		// would double it.
		if (dispatch == LimbUseDispatch.Dispatched)
		{
			PlayTreatmentSound(dragItem, limbIndex);
		}

		return true;
	}

	/// <summary>How one limb-treatment gesture ended: the domain took it, the item's own native action took it, or nothing ran and the caller plays nothing.</summary>
	private enum LimbUseDispatch
	{
		Refused,
		Dispatched,
		DispatchedNative,
	}

	/// <summary>
	/// Route one accepted limb-treatment gesture to the domain that owns it.
	/// Every refusal returns <see cref="LimbUseDispatch.Refused"/>, and the caller
	/// plays no clip for it.
	/// </summary>
	private LimbUseDispatch TryDispatchLimbUse(Item dragItem, int limbIndex, ulong target, ulong itemInstanceId)
	{
		if (RemoteBandageMinigameCatalog.IsBandageItem(dragItem.id))
		{
			return Dispatch(_otherOps.TryStartRemoteBandageUse(dragItem, limbIndex, target, itemInstanceId));
		}

		if (RemoteOtherMedicalCatalog.IsAed(dragItem.id))
		{
			return Dispatch(_otherOps.TryStartRemoteAedUse(dragItem, limbIndex, target, itemInstanceId));
		}

		if (RemoteOtherMedicalCatalog.IsManualDefibrillator(dragItem.id))
		{
			return Dispatch(_otherOps.TryStartRemoteManualDefibUse(dragItem, limbIndex, target, itemInstanceId));
		}

		if (RemoteOtherMedicalCatalog.IsAmputationTool(dragItem.id))
		{
			return Dispatch(_otherOps.TryStartRemoteAmputationUse(dragItem, limbIndex, target, itemInstanceId));
		}

		if (RemoteOtherMedicalCatalog.IsDislocationWrench(dragItem.id)
			&& RemoteMedicalView.DisplayBody is { } dislocationDisplay
			&& limbIndex >= 0
			&& limbIndex < dislocationDisplay.limbs.Length
			&& dislocationDisplay.limbs[limbIndex] is { } wrenchLimb
			&& wrenchLimb.dislocated)
		{
			return Dispatch(_otherOps.TryStartRemoteDislocationUse(wrenchLimb, wrench: true, dragItem, target, itemInstanceId));
		}

		if (RemoteHealProfiles.IsHealItem(dragItem.id))
		{
			_domains.PlayerInteraction.SendHealRequest(target, itemInstanceId, limbIndex);
			_domains.Log.LogInformation("[MedicalView] requested heal of {Target} limb {Limb} with {ItemId} (id {InstanceId}).",
				target, limbIndex, dragItem.id, itemInstanceId);
			return LimbUseDispatch.Dispatched;
		}

		if (LocalUseItemEligibility.IsInjectableRemoteItem(dragItem, _domains.LimbUseSemantics))
		{
			return _injectionOps.TryStart(dragItem, limbIndex, target, itemInstanceId)
				? LimbUseDispatch.DispatchedNative
				: LimbUseDispatch.Refused;
		}

		if (dragItem.id == "tweezers")
		{
			return Dispatch(_shrapnelOps.TryStartRemoteShrapnelUse(dragItem, limbIndex, target, itemInstanceId));
		}

		_domains.PlayerInteraction.SendUseRequest(target, itemInstanceId, limbIndex);
		_domains.Log.LogInformation("[MedicalView] requested use of {Target} limb {Limb} with {ItemId} (id {InstanceId}).",
			target, limbIndex, dragItem.id, itemInstanceId);
		return LimbUseDispatch.Dispatched;
	}

	private static LimbUseDispatch Dispatch(bool handled) =>
		handled ? LimbUseDispatch.Dispatched : LimbUseDispatch.Refused;

	/// <summary>
	/// Play the clip(s) this item's own native limb action would have played, at
	/// the treated limb of the displayed body — the position the peers replay it
	/// at. The item's row and, for a topical container, the applied liquid's row
	/// are separate facts because the native path plays them from separate calls;
	/// an item the census records as natively silent plays nothing.
	/// </summary>
	private void PlayTreatmentSound(Item dragItem, int limbIndex)
	{
		var display = RemoteMedicalView.DisplayBody;
		if (display == null // Unity object — ==
			|| limbIndex < 0
			|| limbIndex >= display.limbs.Length
			|| display.limbs[limbIndex] == null) // Unity object — ==
		{
			return;
		}

		var limb = display.limbs[limbIndex];
		var position = limb.body != null ? limb.body.transform.position : limb.transform.position; // Unity objects — ==

		using var scope = CallContext.Enter(CallContext.Origin.CharacterMedicalUse);

		RemoteMedicalTreatmentSoundCatalog.TryGetClip(dragItem.id, out var clip);
		if (clip is not null)
		{
			Sound.Play(clip, position, false, true, null, 1f, 1f, false, false);
		}

		if (dragItem.GetComponent<WaterContainerItem>() is { } container) // Unity object — ==
		{
			foreach (var liquid in container.stack)
			{
				if (RemoteMedicalTreatmentSoundCatalog.TryGetLiquidClip(liquid.liquidId, out var liquidClip) && liquidClip != clip)
				{
					Sound.Play(liquidClip, position, false, true, null, 1f, 1f, false, false);
				}
			}
		}
	}

	internal bool TryStartRemoteShrapnelSpecial(Limb limb) =>
		_shrapnelOps.TryStartRemoteShrapnelSpecial(limb);

	internal bool TryStartRemoteWoundSpecial(Limb limb)
	{
		if (limb == null) // Unity object — ==
		{
			return false;
		}

		if (limb.GetComponent<TourniquetScript>() != null) // Unity object — ==
		{
			return _otherOps.TryStartRemoteRemoval(limb, MedicalOperationKind.TourniquetRemoval);
		}

		if (limb.hasShrapnel)
		{
			return _shrapnelOps.TryStartRemoteShrapnelSpecial(limb);
		}

		if (limb.GetComponent<SplintLimb>() != null) // Unity object — ==
		{
			return _otherOps.TryStartRemoteRemoval(limb, MedicalOperationKind.SplintRemoval);
		}

		if (limb.dislocated)
		{
			return _otherOps.TryStartRemoteDislocationUse(
				limb,
				wrench: false,
				item: null,
				RemoteMedicalView.TargetSteamId,
				0);
		}

		return false;
	}

	// ---- Injection session forwarding seam ----
	// The syringe session, its dose stream and its completion paths live in
	// RemoteInjectionUseHandler; these forwards keep the existing static call sites
	// (patches, remote view close, medical apply) unchanged.

	internal bool TryDivertInjection(WaterContainerItem container, Limb limb, float amount) =>
		_injectionOps.TryDivertInjection(container, limb, amount);

	internal static void CompleteActiveSyringeUse() => RemoteInjectionUseHandler.CompleteActiveSyringeUse();

	internal static bool CancelActiveSyringeUse() => RemoteInjectionUseHandler.CancelActiveSyringeUse();

	internal static void MarkAuthoritativeItemApplied(ulong itemInstanceId) =>
		RemoteInjectionUseHandler.MarkAuthoritativeItemApplied(itemInstanceId);

	internal static void OnHostTerminal(ulong operationId) =>
		RemoteInjectionUseHandler.OnHostTerminal(operationId);

	// ---- Shrapnel shared-session forwarding seam ----
	// The shrapnel-specific state and native-minigame adapter live in
	// RemoteShrapnelOperationHandler; these small forwards keep the existing
	// static call sites (patches, remote view close, medical apply) unchanged.

	internal static void CompleteActiveShrapnelUse() => RemoteShrapnelOperationHandler.CompleteActiveShrapnelUse();

	internal static bool CancelActiveShrapnelUse() => RemoteShrapnelOperationHandler.CancelActiveShrapnelUse();

	internal static void OnShrapnelHostTerminal(ulong operationId) =>
		RemoteShrapnelOperationHandler.OnShrapnelHostTerminal(operationId);

	internal static void ApplyShrapnelState(MedicalOperationStateMsg msg) =>
		RemoteShrapnelOperationHandler.ApplyShrapnelState(msg);

	internal static void ReportShrapnelUpdate(ShrapnelPieceUpdate update) =>
		RemoteShrapnelOperationHandler.ReportShrapnelUpdate(update);

	internal static bool IsActiveShrapnelMinigame(ShrapnelMinigame minigame) =>
		RemoteShrapnelOperationHandler.IsActiveShrapnelMinigame(minigame);

	internal static bool IsObserverShrapnelMinigame(ShrapnelMinigame minigame) =>
		RemoteShrapnelOperationHandler.IsObserverShrapnelMinigame(minigame);

	internal static bool IsShrapnelPieceOwnedByOther(int pieceIndex) =>
		RemoteShrapnelOperationHandler.IsShrapnelPieceOwnedByOther(pieceIndex);

}
