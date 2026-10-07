using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Applies the host-authoritative half of an incremental injection — the drain
/// of the operator's item and the committed-ml bookkeeping — and builds the
/// non-terminal and terminal wire messages. The EFFECT is not applied here: the
/// drained amounts travel to the target, whose own client runs the game's native
/// <c>WaterContainerItem.Inject</c> path on its own body (Part A of
/// <c>mod-cross-player-native-semantics</c>). It owns no sessions/reservations —
/// that state stays in <see cref="MedicalOperationSessionService"/>.
/// </summary>
internal sealed class MedicalOperationInjectionApplier(
	PlayerCharacterAccess access,
	IItemControl items,
	ItemKernelAuthority kernelAuthority,
	ISessionControl session,
	ILogger log)
{
	private readonly PlayerCharacterAccess _access = access;
	private readonly IItemControl _items = items;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ISessionControl _session = session;
	private readonly ILogger _log = log;

	internal bool TryApplyDelta(OperationSession session, float requested, out MedicalOperationStateMsg? state)
	{
		state = null;
		var remaining = session.AvailableMl - session.CommittedMl;
		if (remaining <= 0f)
		{
			return false;
		}

		var effective = Math.Min(requested, remaining);
		if (effective < 0.01f)
		{
			return false;
		}

		var userData = _access.GetCharacterData(session.Operator);
		var targetData = _access.GetCharacterData(session.Target);
		if (userData is null || targetData?.Health is null)
		{
			_log.LogWarning("[MedicalOps] operation {OperationId} delta skipped: no authoritative snapshot for {Operator}/{Target}.", session.OperationId, session.Operator, session.Target);
			return false;
		}

		var itemIndex = PlayerItemIndex.Find(userData, session.ItemInstanceId);
		if (itemIndex < 0 || itemIndex >= userData.Items.Count)
		{
			_log.LogWarning("[MedicalOps] operation {OperationId} delta skipped: item lost from {Operator}.", session.OperationId, session.Operator);
			return false;
		}

		var newUserData = PlayerCharacterAccess.CloneCharacter(userData);
		var newItem = PlayerCharacterAccess.CloneItem(userData.Items[itemIndex]);

		if (!LiquidDrainPlan.TryCreate(newItem.Liquids, effective, out var plan) || plan.Count == 0)
		{
			_log.LogWarning("[MedicalOps] operation {OperationId} delta skipped: cannot build a drain plan for {ItemId}.", session.OperationId, newItem.ItemId);
			return false;
		}

		PlayerItemUseService.ApplyDrain(newItem, plan);
		newUserData.Items[itemIndex] = newItem;

		_access.SaveCharacterData(session.Operator, newUserData);
		SyncOperatorItemAfterUpdate(session.Operator, newItem);

		session.CommittedMl += effective;
		session.Sequence++;

		state = new MedicalOperationStateMsg
		{
			OperationId = session.OperationId,
			OperatorSteamId = session.Operator,
			TargetSteamId = session.Target,
			ItemInstanceId = session.ItemInstanceId,
			LimbIndex = session.LimbIndex,
			CommittedMl = session.CommittedMl,
			Sequence = session.Sequence,
			ItemAfter = PlayerCharacterAccess.CloneItem(newItem),
			TargetHealth = targetData.Health,
			TargetLimbs = targetData.Limbs,
			AppliedDose = ClonePlan(plan),
		};
		return true;
	}

	/// <summary>
	/// The single terminal message. It carries the final authoritative item and
	/// target snapshot plus the dose of the last unreported delta, which the
	/// target's own client must still apply natively — every earlier delta
	/// travelled on its own non-terminal state message.
	/// </summary>
	internal MedicalOperationEndCommittedMsg BuildTerminal(
		OperationSession session,
		MedicalOperationTerminalReason reason,
		IReadOnlyList<LiquidStackMsg>? finalDose = null)
	{
		var userData = _access.GetCharacterData(session.Operator);
		var targetData = _access.GetCharacterData(session.Target);
		CharacterItemMsg? itemAfter = null;
		CharacterHealthMsg? health = null;
		List<CharacterLimbMsg> limbs = [];

		if (userData is not null)
		{
			var idx = PlayerItemIndex.Find(userData, session.ItemInstanceId);
			if (idx >= 0 && idx < userData.Items.Count)
			{
				itemAfter = PlayerCharacterAccess.CloneItem(userData.Items[idx]);
			}
		}

		if (targetData is not null)
		{
			health = targetData.Health;
			limbs = [.. targetData.Limbs];
		}

		return new MedicalOperationEndCommittedMsg
		{
			OperationId = session.OperationId,
			OperatorSteamId = session.Operator,
			TargetSteamId = session.Target,
			ItemInstanceId = session.ItemInstanceId,
			LimbIndex = session.LimbIndex,
			CommittedMl = session.CommittedMl,
			TerminalReason = reason,
			ItemAfter = itemAfter,
			TargetHealth = health,
			TargetLimbs = limbs,
			AppliedDose = finalDose is null ? [] : ClonePlan(finalDose),
		};
	}

	private static List<LiquidStackMsg> ClonePlan(IReadOnlyList<LiquidStackMsg> plan) =>
		[.. plan.Select(l => new LiquidStackMsg { LiquidId = l.LiquidId, Amount = l.Amount })];

	private void SyncOperatorItemAfterUpdate(ulong operatorSteamId, CharacterItemMsg item)
	{
		if (operatorSteamId != _session.LocalSteamId)
		{
			_items.UpdateTransferredItem(operatorSteamId, item.InstanceId, PlayerCharacterAccess.CloneItem(item));
			return;
		}

		var current = _kernelAuthority.FindItem(item.InstanceId);
		if (current is null)
		{
			_kernelAuthority.TrySpawnCarried(_session.LocalSteamId, item.InstanceId, item.ItemId, item, out _, out _);
		}
		else
		{
			_kernelAuthority.TryUpdateState(_session.LocalSteamId, item.InstanceId, item, out _, out _);
		}
	}

}
