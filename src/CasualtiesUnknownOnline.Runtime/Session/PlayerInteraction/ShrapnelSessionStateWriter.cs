using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure state projection for the shared shrapnel session: initial piece
/// layout, authoritative limb/item side effects and the wire State/End
/// payloads. It owns no mutable session state; the
/// <see cref="ShrapnelOperationSessionService"/> keeps the registry and
/// arbitration lifecycle.
/// </summary>
internal sealed class ShrapnelSessionStateWriter(
	PlayerCharacterAccess access,
	IItemControl items,
	ItemKernelAuthority kernelAuthority,
	ISessionControl session,
	ILogger log)
{
	private const int MaxPieces = 5;
	private const float RemoveThresholdY = 35f;
	private const float TweezersConditionCost = 0.01f;
	private static readonly float[] DefaultX = [-248f, -143f, 0f, 116f, 208f];
	private const float DefaultY = -316.85f;

	private readonly PlayerCharacterAccess _access = access;
	private readonly IItemControl _items = items;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ISessionControl _session = session;
	private readonly ILogger _log = log;

	internal void InitializePieces(ShrapnelOperationSession shrapnel, int activeCount)
	{
		for (var i = 0; i < MaxPieces; i++)
		{
			shrapnel.Pieces[i] = new ShrapnelPieceState
			{
				PieceIndex = i,
				X = DefaultX[i],
				Y = DefaultY,
				Removed = i >= activeCount,
			};
		}
	}

	internal void DrainTweezers(ulong operatorId, ulong itemInstanceId, CharacterDataMsg userData)
	{
		var newData = PlayerCharacterAccess.CloneCharacter(userData);
		var index = FindUseItemIndex(newData, itemInstanceId);
		if (index < 0 || index >= newData.Items.Count)
		{
			return;
		}

		var newItem = PlayerCharacterAccess.CloneItem(newData.Items[index]);
		newItem.Condition = Math.Max(0f, newItem.Condition - TweezersConditionCost);
		newData.Items[index] = newItem;
		_access.SaveCharacterData(operatorId, newData);

		if (operatorId != _session.LocalSteamId)
		{
			_items.UpdateTransferredItem(operatorId, itemInstanceId, PlayerCharacterAccess.CloneItem(newItem));
		}
		else
		{
			var current = _kernelAuthority.FindItem(itemInstanceId);
			if (current is null)
			{
				_kernelAuthority.TrySpawnCarried(_session.LocalSteamId, itemInstanceId, newItem.ItemId, newItem, out _, out _);
			}
			else
			{
				_kernelAuthority.TryUpdateState(_session.LocalSteamId, itemInstanceId, newItem, out _, out _);
			}
		}
	}

	internal void ApplyBreakGrasp(ShrapnelOperationSession shrapnel)
	{
		var targetData = _access.GetCharacterData(shrapnel.Target);
		if (targetData is null || shrapnel.LimbIndex < 0 || shrapnel.LimbIndex >= targetData.Limbs.Count)
		{
			return;
		}

		var newData = PlayerCharacterAccess.CloneCharacter(targetData);
		var limb = newData.Limbs[shrapnel.LimbIndex];
		limb.SkinHealth = Math.Max(0f, limb.SkinHealth - RandomInRange(4f, 6f));
		limb.BleedAmount = Math.Max(0f, limb.BleedAmount + RandomInRange(0.4f, 1f));
		limb.Pain = Math.Max(0f, limb.Pain + RandomInRange(9f, 16f));
		if (limb.IsHead && newData.Health is not null)
		{
			newData.Health.BrainHealth = Math.Max(0f, newData.Health.BrainHealth - RandomInRange(0f, 1f));
		}

		_access.SaveCharacterData(shrapnel.Target, newData);
		_log.LogWarning("[Shrapnel] session {OperationId} break-grasp applied to {Target} limb {Limb}.",
			shrapnel.OperationId, shrapnel.Target, shrapnel.LimbIndex);
	}

	internal void UpdateAuthoritativeLimb(ShrapnelOperationSession shrapnel)
	{
		var targetData = _access.GetCharacterData(shrapnel.Target);
		if (targetData is null || shrapnel.LimbIndex < 0 || shrapnel.LimbIndex >= targetData.Limbs.Count)
		{
			return;
		}

		var newData = PlayerCharacterAccess.CloneCharacter(targetData);
		var limb = newData.Limbs[shrapnel.LimbIndex];
		limb.Shrapnel = shrapnel.Pieces.Values.Count(p => !p.Removed);
		_access.SaveCharacterData(shrapnel.Target, newData);
	}

	internal MedicalOperationStateMsg BuildState(ShrapnelOperationSession shrapnel)
	{
		var targetData = _access.GetCharacterData(shrapnel.Target);
		return new MedicalOperationStateMsg
		{
			OperationId = shrapnel.OperationId,
			OperatorSteamId = shrapnel.Operators.FirstOrDefault(),
			TargetSteamId = shrapnel.Target,
			LimbIndex = shrapnel.LimbIndex,
			Sequence = shrapnel.Sequence,
			ShrapnelPieces = BuildPieces(shrapnel),
			TargetHealth = targetData?.Health,
			TargetLimbs = targetData?.Limbs is null ? [] : [.. targetData.Limbs],
		};
	}

	internal MedicalOperationEndCommittedMsg BuildTerminal(ShrapnelOperationSession shrapnel, MedicalOperationTerminalReason reason)
	{
		var targetData = _access.GetCharacterData(shrapnel.Target);
		return new MedicalOperationEndCommittedMsg
		{
			OperationId = shrapnel.OperationId,
			OperatorSteamId = shrapnel.Operators.FirstOrDefault(),
			TargetSteamId = shrapnel.Target,
			LimbIndex = shrapnel.LimbIndex,
			TerminalReason = reason,
			ShrapnelPieces = BuildPieces(shrapnel),
			TargetHealth = targetData?.Health,
			TargetLimbs = targetData?.Limbs is null ? [] : [.. targetData.Limbs],
		};
	}

	internal static List<ShrapnelPieceMsg> BuildPieces(ShrapnelOperationSession shrapnel) =>
	[.. shrapnel.Pieces.Values.OrderBy(p => p.PieceIndex).Select(p => new ShrapnelPieceMsg
	{
		PieceIndex = p.PieceIndex,
		X = p.X,
		Y = p.Y,
		OwnerSteamId = p.Owner,
		Removed = p.Removed,
	})];

	private static int FindUseItemIndex(CharacterDataMsg data, ulong itemInstanceId)
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

	private static float RandomInRange(float min, float max) => (float)(new Random().NextDouble() * (max - min) + min);
}
