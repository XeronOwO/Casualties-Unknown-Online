using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Applies the host-authoritative incremental injection mathematics to the
/// character/item snapshots and builds the non-terminal and terminal wire
/// messages. It owns no sessions/reservations — that state stays in
/// <see cref="MedicalOperationSessionService"/>.
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
		var newTargetData = PlayerCharacterAccess.CloneCharacter(targetData);

		if (!RemoteMedicineCatalog.TryCreatePlan(newItem.Liquids, newItem.ItemId, effective, out var plan) || plan.Count == 0)
		{
			_log.LogWarning("[MedicalOps] operation {OperationId} delta skipped: cannot build medicine plan for {ItemId}.", session.OperationId, newItem.ItemId);
			return false;
		}

		RemoteMedicineApplication.Apply(newTargetData.Health!, newTargetData.Limbs, plan, session.LimbIndex);
		PlayerItemUseService.ApplyDrain(newItem, plan);
		newUserData.Items[itemIndex] = newItem;

		_access.SaveCharacterData(session.Operator, newUserData);
		_access.SaveCharacterData(session.Target, newTargetData);
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
			TargetHealth = newTargetData.Health,
			TargetLimbs = newTargetData.Limbs,
		};
		return true;
	}

	internal MedicalOperationEndCommittedMsg BuildTerminal(OperationSession session, MedicalOperationTerminalReason reason)
	{
		var userData = _access.GetCharacterData(session.Operator);
		var targetData = _access.GetCharacterData(session.Target);
		CharacterItemMsg? itemAfter = null;
		CharacterHealthMsg? health = null;
		List<CharacterLimbMsg> limbs = [];
		var timedEffects = new List<TimedBodyEffectMsg>();

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
			if (session.CommittedMl > 0f
				&& RemoteMedicineCatalog.TryCreatePlan(session.OriginalLiquids, session.ItemId, session.CommittedMl, out var fullPlan))
			{
				timedEffects = RemoteMedicineApplication.BuildTimedEffects(fullPlan);
			}
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
			TimedBodyEffects = timedEffects,
		};
	}

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
