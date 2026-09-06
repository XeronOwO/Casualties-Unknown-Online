using System;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Host-side state projection for the Stage 3 medical operations. It applies
/// the authoritative partial/final effects to the character/item snapshots and
/// builds the wire State/End payloads. It owns no session registry — that
/// lives in <see cref="OtherMedicalOperationSessionService"/>.
/// </summary>
internal sealed class OtherMedicalOperationApplier(
	PlayerCharacterAccess access,
	IItemControl items,
	ItemKernelAuthority kernelAuthority,
	ISessionControl session)
{
	private const float AmputationDamagePerCut = 112.5f;

	private readonly PlayerCharacterAccess _access = access;
	private readonly IItemControl _items = items;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ISessionControl _session = session;
	private static readonly Random Rng = new();

	internal bool ApplyBandageWrap(OtherMedicalOperationSession session, out MedicalOperationStateMsg? state)
	{
		state = null;
		if (!RemoteBandageMinigameCatalog.TryGet(session.ItemId, out var profile, out var turns)
			|| turns <= 0f)
		{
			return false;
		}

		var operatorData = _access.GetCharacterData(session.Operator);
		var targetData = _access.GetCharacterData(session.Target);
		if (operatorData is null || targetData is null || session.LimbIndex < 0 || session.LimbIndex >= targetData.Limbs.Count)
		{
			return false;
		}

		var itemIndex = FindItemIndex(operatorData, session.ItemInstanceId);
		if (itemIndex < 0 || itemIndex >= operatorData.Items.Count)
		{
			return false;
		}

		var newOperator = PlayerCharacterAccess.CloneCharacter(operatorData);
		var newItem = PlayerCharacterAccess.CloneItem(newOperator.Items[itemIndex]);
		var newTarget = PlayerCharacterAccess.CloneCharacter(targetData);
		var limb = newTarget.Limbs[session.LimbIndex];
		if (limb.Dismembered)
		{
			return false;
		}

		var wrapProgress = 1f / (18f * turns);
		var remaining = session.Progress < 1f ? 1f - session.Progress : 0f;
		if (remaining <= 0f || wrapProgress > remaining)
		{
			return false;
		}

		var applyScale = wrapProgress;
		ApplyScaled(newTarget.Health!, limb, profile, applyScale);
		newItem.Condition = Math.Max(0f, newItem.Condition - profile.ConditionCost * applyScale);
		session.Progress += wrapProgress;

		var destroyed = newItem.Condition <= 0f;
		if (destroyed)
		{
			newOperator.Items.RemoveAll(i => i.InstanceId == session.ItemInstanceId);
		}
		else
		{
			newOperator.Items[itemIndex] = newItem;
		}

		_access.SaveCharacterData(session.Operator, newOperator);
		_access.SaveCharacterData(session.Target, newTarget);
		SyncOperatorItem(session.Operator, newItem, destroyed);

		state = BuildState(session, newOperator, newTarget, destroyed ? null : newItem);
		return true;
	}

	internal bool ApplyDislocationHit(OtherMedicalOperationSession session, bool wrench, out MedicalOperationStateMsg? state)
	{
		state = null;
		var targetData = _access.GetCharacterData(session.Target);
		if (targetData is null || session.LimbIndex < 0 || session.LimbIndex >= targetData.Limbs.Count)
		{
			return false;
		}

		var newTarget = PlayerCharacterAccess.CloneCharacter(targetData);
		var limb = newTarget.Limbs[session.LimbIndex];
		if (!limb.Dislocated)
		{
			return false;
		}

		limb.Pain = Math.Max(0f, limb.Pain + (wrench ? RandomRange(4f, 10f) : RandomRange(15f, 24f)));
		_access.SaveCharacterData(session.Target, newTarget);

		state = BuildState(session, null, newTarget, null);
		return true;
	}

	internal bool ApplyAmputationCut(OtherMedicalOperationSession session, float delta, out MedicalOperationStateMsg? state)
	{
		state = null;
		var targetData = _access.GetCharacterData(session.Target);
		if (targetData is null || session.LimbIndex < 0 || session.LimbIndex >= targetData.Limbs.Count)
		{
			return false;
		}

		var newTarget = PlayerCharacterAccess.CloneCharacter(targetData);
		var limb = newTarget.Limbs[session.LimbIndex];
		if (limb.Dismembered || delta <= 0f)
		{
			return false;
		}

		var effective = Math.Min(delta, Math.Max(0f, 1f - session.Progress));
		if (effective <= 0f)
		{
			return false;
		}

		session.Progress += effective;
		limb.Pain = Math.Max(0f, limb.Pain + effective * AmputationDamagePerCut * 0.5f);
		limb.SkinHealth = Clamp100(limb.SkinHealth - effective * AmputationDamagePerCut);
		limb.MuscleHealth = Clamp100(limb.MuscleHealth - effective * AmputationDamagePerCut);
		limb.BleedAmount = Math.Max(0f, limb.BleedAmount + effective * AmputationDamagePerCut * 0.5f);
		_access.SaveCharacterData(session.Target, newTarget);

		state = BuildState(session, null, newTarget, null);
		return true;
	}

	internal void CompleteAmputation(OtherMedicalOperationSession session)
	{
		var targetData = _access.GetCharacterData(session.Target);
		if (targetData is null || session.LimbIndex < 0 || session.LimbIndex >= targetData.Limbs.Count)
		{
			return;
		}

		var newTarget = PlayerCharacterAccess.CloneCharacter(targetData);
		var limb = newTarget.Limbs[session.LimbIndex];
		if (limb.Dismembered)
		{
			return;
		}

		limb.Dismembered = true;
		limb.SkinHealth = 0f;
		limb.MuscleHealth = 0f;
		limb.BleedAmount = 0f;
		limb.Pain = 100f;
		limb.Infected = false;
		limb.InfectionAmount = 0f;

		foreach (var connectedIndex in limb.ConnectedLimbIndices)
		{
			if (connectedIndex < 0 || connectedIndex >= newTarget.Limbs.Count)
			{
				continue;
			}

			var connected = newTarget.Limbs[connectedIndex];
			connected.Infected = false;
			connected.InfectionAmount = 0f;
			connected.BleedAmount *= 0.5f;
		}

		if (newTarget.Health is { } health)
		{
			health.TraumaAmount = Math.Max(0f, health.TraumaAmount - 20f);
		}

		_access.SaveCharacterData(session.Target, newTarget);
	}

	internal bool CompleteDislocation(OtherMedicalOperationSession session, bool succeeded)
	{
		if (!succeeded)
		{
			return false;
		}

		var targetData = _access.GetCharacterData(session.Target);
		if (targetData is null || session.LimbIndex < 0 || session.LimbIndex >= targetData.Limbs.Count)
		{
			return false;
		}

		var newTarget = PlayerCharacterAccess.CloneCharacter(targetData);
		var limb = newTarget.Limbs[session.LimbIndex];
		if (!limb.Dislocated)
		{
			return false;
		}

		limb.Dislocated = false;
		limb.DislocationTimer = 0f;
		_access.SaveCharacterData(session.Target, newTarget);
		return true;
	}

	internal bool ApplyAedStage(OtherMedicalOperationSession session, MedicalOperationUpdateAction action, float stageCode, out MedicalOperationStateMsg? state)
	{
		state = null;
		var operatorData = _access.GetCharacterData(session.Operator);
		if (operatorData is null)
		{
			return false;
		}

		var itemIndex = FindItemIndex(operatorData, session.ItemInstanceId);
		if (itemIndex < 0 || itemIndex >= operatorData.Items.Count)
		{
			return false;
		}

		var newOperator = PlayerCharacterAccess.CloneCharacter(operatorData);
		var newItem = PlayerCharacterAccess.CloneItem(newOperator.Items[itemIndex]);

		if (action == MedicalOperationUpdateAction.Shock)
		{
			if (session.AedShockApplied)
			{
				return false;
			}

			newItem.Condition = Math.Max(0f, newItem.Condition - 0.14f);
			session.AedShockApplied = true;
			ApplyDefibrillation(session, chance: 1f);
		}
		else if (action == MedicalOperationUpdateAction.Stage && stageCode <= 0.5f)
		{
			if (session.AedStartDrained)
			{
				return false;
			}

			newItem.Condition = Math.Max(0f, newItem.Condition - 0.01f);
			session.AedStartDrained = true;
		}
		else if (action == MedicalOperationUpdateAction.Stage && stageCode > 0.5f)
		{
			if (session.AedAnalyzeDrained)
			{
				return false;
			}

			newItem.Condition = Math.Max(0f, newItem.Condition - 0.02f);
			session.AedAnalyzeDrained = true;
		}
		else
		{
			return false;
		}

		var destroyed = newItem.Condition <= 0f;
		if (destroyed)
		{
			newOperator.Items.RemoveAll(i => i.InstanceId == session.ItemInstanceId);
		}
		else
		{
			newOperator.Items[itemIndex] = newItem;
		}

		_access.SaveCharacterData(session.Operator, newOperator);
		SyncOperatorItem(session.Operator, newItem, destroyed: destroyed);

		var targetData = _access.GetCharacterData(session.Target);
		state = BuildState(session, newOperator, targetData, destroyed ? null : newItem);
		return true;
	}

	internal bool ApplyManualShock(OtherMedicalOperationSession session, float charge, out MedicalOperationStateMsg? state)
	{
		state = null;
		var operatorData = _access.GetCharacterData(session.Operator);
		if (operatorData is null)
		{
			return false;
		}

		var itemIndex = FindItemIndex(operatorData, session.ItemInstanceId);
		if (itemIndex < 0 || itemIndex >= operatorData.Items.Count)
		{
			return false;
		}

		var newOperator = PlayerCharacterAccess.CloneCharacter(operatorData);
		var newItem = PlayerCharacterAccess.CloneItem(newOperator.Items[itemIndex]);
		newItem.Condition = Math.Max(0f, newItem.Condition - charge / 4000f);
		var destroyed = newItem.Condition <= 0f;
		if (destroyed)
		{
			newOperator.Items.RemoveAll(i => i.InstanceId == session.ItemInstanceId);
		}
		else
		{
			newOperator.Items[itemIndex] = newItem;
		}

		_access.SaveCharacterData(session.Operator, newOperator);
		SyncOperatorItem(session.Operator, newItem, destroyed: destroyed);

		var targetData = _access.GetCharacterData(session.Target);
		var fibrillation = targetData?.Health?.FibrillationProgress ?? 0f;
		var chance = Math.Max(0f, Math.Min(1f, 1f - Math.Abs(fibrillation - charge * 0.5f) / 40f));
		ApplyDefibrillation(session, chance);

		state = BuildState(session, newOperator, _access.GetCharacterData(session.Target), destroyed ? null : newItem);
		return true;
	}

	internal void DrainManualDefibTime(OtherMedicalOperationSession session, long nowMs)
	{
		var elapsedSeconds = Math.Max(0f, (nowMs - session.StartedMs) / 1000f - session.ManualDefibSeconds);
		if (elapsedSeconds <= 0f)
		{
			return;
		}

		var operatorData = _access.GetCharacterData(session.Operator);
		if (operatorData is null)
		{
			return;
		}

		var itemIndex = FindItemIndex(operatorData, session.ItemInstanceId);
		if (itemIndex < 0)
		{
			return;
		}

		var newOperator = PlayerCharacterAccess.CloneCharacter(operatorData);
		var newItem = PlayerCharacterAccess.CloneItem(newOperator.Items[itemIndex]);
		newItem.Condition = Math.Max(0f, newItem.Condition - elapsedSeconds / 800f);
		var destroyed = newItem.Condition <= 0f;
		if (destroyed)
		{
			newOperator.Items.RemoveAll(i => i.InstanceId == session.ItemInstanceId);
		}
		else
		{
			newOperator.Items[itemIndex] = newItem;
		}

		_access.SaveCharacterData(session.Operator, newOperator);
		SyncOperatorItem(session.Operator, newItem, destroyed: destroyed);
		session.ManualDefibSeconds += elapsedSeconds;
	}

	internal MedicalOperationEndCommittedMsg BuildTerminal(OtherMedicalOperationSession session, MedicalOperationTerminalReason reason, bool dislocateSucceeded = false)
	{
		if (session.Kind == MedicalOperationKind.Amputation && session.Progress >= 1f)
		{
			CompleteAmputation(session);
		}

		if (session.Kind == MedicalOperationKind.Dislocation)
		{
			CompleteDislocation(session, dislocateSucceeded);
		}

		var targetData = _access.GetCharacterData(session.Target);
		var operatorData = _access.GetCharacterData(session.Operator);
		var itemAfter = operatorData is null ? null : FindItemAfter(operatorData, session.ItemInstanceId);
		var awarded = session.Kind is MedicalOperationKind.SplintRemoval or MedicalOperationKind.TourniquetRemoval
			? FindAwardedItem(operatorData, session)
			: null;

		return new MedicalOperationEndCommittedMsg
		{
			OperationId = session.OperationId,
			OperatorSteamId = session.Operator,
			TargetSteamId = session.Target,
			ItemInstanceId = session.ItemInstanceId,
			LimbIndex = session.LimbIndex,
			TerminalReason = reason,
			Kind = session.Kind,
			ActionProgress = session.Progress,
			ItemAfter = itemAfter,
			AwardedItem = awarded,
			TargetHealth = targetData?.Health,
			TargetLimbs = targetData?.Limbs is null ? [] : [.. targetData.Limbs],
		};
	}

	internal MedicalOperationStateMsg BuildState(
		OtherMedicalOperationSession session,
		CharacterDataMsg? operatorData,
		CharacterDataMsg? targetData,
		CharacterItemMsg? itemAfter)
	{
		return new MedicalOperationStateMsg
		{
			OperationId = session.OperationId,
			OperatorSteamId = session.Operator,
			TargetSteamId = session.Target,
			ItemInstanceId = session.ItemInstanceId,
			LimbIndex = session.LimbIndex,
			Kind = session.Kind,
			ActionProgress = session.Progress,
			Sequence = session.Sequence + 1,
			ItemAfter = itemAfter,
			TargetHealth = targetData?.Health,
			TargetLimbs = targetData?.Limbs is null ? [] : [.. targetData.Limbs],
		};
	}

	private void ApplyDefibrillation(OtherMedicalOperationSession session, float chance)
	{
		var targetData = _access.GetCharacterData(session.Target);
		if (targetData is null || session.LimbIndex < 0 || session.LimbIndex >= targetData.Limbs.Count)
		{
			return;
		}

		var newTarget = PlayerCharacterAccess.CloneCharacter(targetData);
		var limb = newTarget.Limbs[session.LimbIndex];
		limb.SkinHealth = Math.Max(0f, limb.SkinHealth - 5f);
		limb.Pain = Math.Max(0f, limb.Pain + 20f);

		if (newTarget.Health is { } health && limb.Index == 1 && health.Alive)
		{
			if (chance >= 1f || Rng.NextDouble() < chance)
			{
				health.FibrillationProgress = 0f;
				health.BloodPressure = Math.Max(0f, health.BloodPressure * 0.5f);
			}
		}

		_access.SaveCharacterData(session.Target, newTarget);
	}

	private void SyncOperatorItem(ulong operatorId, CharacterItemMsg item, bool destroyed)
	{
		if (destroyed)
		{
			if (operatorId == _session.LocalSteamId)
			{
				var current = _kernelAuthority.FindItem(item.InstanceId);
				if (current is not null)
				{
					_kernelAuthority.TryDestroy(_session.LocalSteamId, item.InstanceId, TerminalKind.Consumed, out _, out _);
				}
			}
			else
			{
				_items.RemoveTransferredItem(operatorId, item.InstanceId);
				var kernel = _kernelAuthority.FindItem(item.InstanceId);
				if (kernel is not null)
				{
					_kernelAuthority.TryDestroy(_session.LocalSteamId, item.InstanceId, TerminalKind.Consumed, out _, out _);
				}
			}

			return;
		}

		if (operatorId == _session.LocalSteamId)
		{
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
		else
		{
			_items.UpdateTransferredItem(operatorId, item.InstanceId, PlayerCharacterAccess.CloneItem(item));
		}
	}

	private CharacterItemMsg? FindAwardedItem(CharacterDataMsg? operatorData, OtherMedicalOperationSession session)
	{
		if (operatorData is null || session.AwardedItemId == 0)
		{
			return null;
		}

		var index = FindItemIndex(operatorData, session.AwardedItemId);
		return index < 0 || index >= operatorData.Items.Count ? null : PlayerCharacterAccess.CloneItem(operatorData.Items[index]);
	}

	private static int FindItemIndex(CharacterDataMsg data, ulong itemInstanceId)
	{
		for (var i = 0; i < data.Items.Count; i++)
		{
			if (data.Items[i].InstanceId == itemInstanceId)
			{
				return i;
			}
		}

		return -1;
	}

	private static CharacterItemMsg? FindItemAfter(CharacterDataMsg data, ulong itemId)
	{
		if (itemId == 0)
		{
			return null;
		}

		var index = FindItemIndex(data, itemId);
		return index < 0 || index >= data.Items.Count ? null : PlayerCharacterAccess.CloneItem(data.Items[index]);
	}

	private static void ApplyScaled(
		CharacterHealthMsg health,
		CharacterLimbMsg limb,
		RemoteHealProfile profile,
		float scale)
	{
		limb.SkinHealAmount = Math.Max(0f, limb.SkinHealAmount + profile.SkinHealAmount * scale);
		limb.BandageSlowAmount = Math.Max(0f, limb.BandageSlowAmount + profile.BandageSlowAmount * scale);
		limb.Pain = Math.Max(0f, limb.Pain + profile.Pain * scale);
		limb.BoneHealTimer = Math.Max(0f, limb.BoneHealTimer + profile.BoneHealTimer * scale);
		limb.DislocationTimer = Math.Max(0f, limb.DislocationTimer + profile.DislocationTimer * scale);
		limb.DisinfectionTime = Math.Max(0f, limb.DisinfectionTime + profile.DisinfectionTime * scale);
		limb.BleedAmount = Math.Max(0f, limb.BleedAmount + profile.BleedAmount * scale);
		limb.SkinHealth = Clamp100(limb.SkinHealth + profile.SkinHealth * scale);
		limb.MuscleHealth = Clamp100(limb.MuscleHealth + profile.MuscleHealth * scale);
		health.OpiateAmount = Math.Max(0f, health.OpiateAmount + profile.OpiateAmount * scale);
	}

	private static float Clamp100(float value) => Math.Max(0f, Math.Min(100f, value));

	private static float RandomRange(float min, float max) => (float)(Rng.NextDouble() * (max - min) + min);
}
