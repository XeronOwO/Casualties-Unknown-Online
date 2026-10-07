using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Local apply side of the remote medical operation session. It consumes the
/// host's non-terminal progress and the single terminal EndCommitted: the
/// operator's own item is drained to the authoritative post-delta state, the
/// target's own body receives the authoritative health/limb progression, and
/// any open remote WoundView display is refreshed for third-party observation.
/// </summary>
internal sealed class MedicalOperationApply(GameAdapterDomains domains)
{
	public void OnStateReceived(MedicalOperationStateMsg msg)
	{
		if (msg.ShrapnelPieces.Count > 0)
		{
			RemoteMedicalOperationHandler.ApplyShrapnelState(msg);
		}

		ApplyProgress(
			msg.OperationId,
			msg.OperatorSteamId,
			msg.TargetSteamId,
			msg.ItemInstanceId,
			msg.LimbIndex,
			msg.Kind,
			msg.ItemAfter,
			msg.TargetHealth,
			msg.TargetLimbs,
			msg.AppliedDose,
			null,
			terminal: false);
	}

	public void OnEndCommittedReceived(MedicalOperationEndCommittedMsg msg)
	{
		if (msg.TerminalReason == MedicalOperationTerminalReason.AlreadyHandled)
		{
			// Another operator settled this unit first. OnHostTerminal (below) ends this
			// client's minigame, and this is the precise answer the operator is given.
			domains.Log.LogInformation(
				"[MedicalOps] operation {OperationId} ({Kind}) on {Target} limb {Limb} stopped: {Reason}",
				msg.OperationId, msg.Kind, msg.TargetSteamId, msg.LimbIndex, MedicalOperationUnitRules.HandledReason(msg.Kind));
		}

		if (msg.ShrapnelPieces.Count > 0)
		{
			RemoteMedicalOperationHandler.OnShrapnelHostTerminal(msg.OperationId);
		}

		ApplyProgress(
			msg.OperationId,
			msg.OperatorSteamId,
			msg.TargetSteamId,
			msg.ItemInstanceId,
			msg.LimbIndex,
			msg.Kind,
			msg.ItemAfter,
			msg.TargetHealth,
			msg.TargetLimbs,
			msg.AppliedDose,
			msg.AwardedItem,
			terminal: true);
	}

	private void ApplyProgress(
		ulong operationId,
		ulong operatorSteamId,
		ulong targetSteamId,
		ulong itemInstanceId,
		int limbIndex,
		MedicalOperationKind kind,
		CharacterItemMsg? itemAfter,
		CharacterHealthMsg? health,
		IReadOnlyList<CharacterLimbMsg> limbs,
		IReadOnlyList<LiquidStackMsg> appliedDose,
		CharacterItemMsg? awardedItem,
		bool terminal)
	{
		if (terminal)
		{
			RemoteMedicalOperationHandler.OnHostTerminal(operationId);
			RemoteOtherMedicalOperationHandler.OnHostTerminal(operationId);
		}

		if (health is null && itemAfter is null && appliedDose.Count == 0)
		{
			return;
		}

		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			domains.Log.LogWarning("[MedicalOps] state/end received but the local body is not ready — skipped.");
			return;
		}

		var changed = false;
		var targetLocal = targetSteamId == domains.Session.LocalSteamId;
		var operatorLocal = operatorSteamId == domains.Session.LocalSteamId;

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (operatorLocal && itemAfter is { } after)
			{
				var item = CarriedItemLocator.FindById(body, itemInstanceId);
				if (item == null) // Unity object — ==
				{
					domains.Log.LogWarning("[MedicalOps] local operator item {ItemId} not found — authoritative item update skipped.", itemInstanceId);
				}
				else
				{
					item.condition = after.Condition;
					ItemStateCodec.RestoreLiquids(item, after.Liquids);
					ItemStateCodec.RestoreComponentStates(item, after.Components);
					RemoteMedicalOperationHandler.MarkAuthoritativeItemApplied(itemInstanceId);
					RemoteOtherMedicalItemRestore.MarkApplied(itemInstanceId);
					domains.Log.LogInformation("[MedicalOps] local item {ItemId} updated to condition {Condition:F2}.", itemInstanceId, after.Condition);
					changed = true;
				}
			}

			if (operatorLocal && awardedItem is { } awarded)
			{
				RestoreAwardedItem(body, awarded);
				RemoteOtherMedicalItemRestore.MarkApplied(awarded.InstanceId);
				changed = true;
			}

			if (targetLocal && kind == MedicalOperationKind.Injection)
			{
				// The EFFECT belongs to this side (Part A of
				// mod-cross-player-native-semantics): the host decided the
				// operation, owns the item and committed the drain, and this
				// client runs the game's own injection path on its own body with
				// the dose it just received. The host's copy of a guest body is
				// report-driven anyway, so its echoed snapshot is deliberately NOT
				// written over a native effect it has not seen yet — the immediate
				// re-report below carries the result back to every peer.
				if (appliedDose.Count > 0)
				{
					var handled = NativeInjectionApply.Apply(body, limbIndex, appliedDose, domains.Log);
					changed |= handled > 0;
					domains.Log.LogInformation(
						"[MedicalOps] operation {OperationId} applied a native injection dose ({Liquids} liquid(s), {Handled} in the registry, terminal={Terminal}).",
						operationId, appliedDose.Count, handled, terminal);
				}
			}
			else if (targetLocal && health is { } targetHealth)
			{
				domains.CharacterDataSync.ApplyHealState(body, targetHealth, limbs);
				domains.Log.LogInformation("[MedicalOps] local target body updated (terminal={Terminal}).", terminal);
				changed = true;
			}
		}

		// Any side with the remote medical view open sees the live progression
		// immediately, even if this client is neither the operator nor target.
		// The fact table is also advanced so the coordinator's next frame does
		// not overwrite live progress with a stale 1 Hz snapshot.
		//
		// The injection family is the exception, and for the same reason the
		// family's own body write above is skipped: the host no longer applies the
		// effect, so THIS message's TargetHealth/TargetLimbs are the target's last
		// report — a snapshot from BEFORE the dose it carries. Neither sink has a
		// staleness guard (CloneFactTable.ApplyMedicalState and
		// RemoteMedicalCoordinator.ApplyMedicalState both write straight through),
		// so feeding them this echo would REPLACE the display's live values with
		// pre-injection ones on every delta. The display therefore advances from
		// the target's own reports, exactly like every other state of a remote
		// body; the other families keep the host-computed snapshot they still own.
		if (kind != MedicalOperationKind.Injection)
		{
			domains.CharacterDataSync.ApplyMedicalState(targetSteamId, health, limbs);
			domains.RemoteMedical.ApplyMedicalState(targetSteamId, health, limbs);
		}

		if (changed)
		{
			domains.CharacterDataSync.ReportInventoryChanged(body);
		}
	}

	private void RestoreAwardedItem(Body body, CharacterItemMsg item)
	{
		var existing = CarriedItemLocator.FindById(body, item.InstanceId);
		if (existing != null) // Unity object — ==
		{
			existing.condition = item.Condition;
			ItemStateCodec.RestoreLiquids(existing, item.Liquids);
			ItemStateCodec.RestoreComponentStates(existing, item.Components);
			domains.Log.LogInformation("[MedicalOps] local awarded item {ItemId} (id {InstanceId}) already present; condition updated.",
				item.ItemId, item.InstanceId);
			return;
		}

		if (item.SlotIndex < 0 || item.SlotIndex >= body.slots.Length || body.HoldingItem(item.SlotIndex))
		{
			var empty = body.FirstEmptySlot();
			if (empty is not { } fallback)
			{
				domains.Log.LogWarning("[MedicalOps] cannot hand awarded {ItemId} (id {InstanceId}) — no empty local slot.",
					item.ItemId, item.InstanceId);
				return;
			}

			item.SlotIndex = fallback;
		}

		ItemStateCodec.RestoreItem(item, body);
		domains.Log.LogInformation("[MedicalOps] local operator awarded {ItemId} (id {InstanceId}) in slot {Slot}.",
			item.ItemId, item.InstanceId, item.SlotIndex);
	}
}
