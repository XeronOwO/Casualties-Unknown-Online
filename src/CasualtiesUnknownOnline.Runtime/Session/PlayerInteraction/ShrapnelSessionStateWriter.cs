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
		var index = PlayerItemIndex.Find(newData, itemInstanceId);
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

	/// <summary>
	/// Whether the session's treated limb is the target's head. It is the one fact the derived draw
	/// needs from the authoritative body, because the native <c>BreakGrasp</c> draws the brain roll
	/// only for a head limb.
	/// </summary>
	internal bool IsHeadLimb(ShrapnelOperationSession shrapnel)
	{
		var targetData = _access.GetCharacterData(shrapnel.Target);
		return targetData is not null
			&& shrapnel.LimbIndex >= 0
			&& shrapnel.LimbIndex < targetData.Limbs.Count
			&& targetData.Limbs[shrapnel.LimbIndex].IsHead;
	}

	/// <summary>
	/// Writes one grip slip's values onto the authoritative limb. The values arrive already drawn (see
	/// <see cref="ShrapnelGripSlipRoller"/>): this type owns the write, not the roll, so the authority's
	/// numbers and the operator's screen come from ONE derivation instead of two.
	/// <para>
	/// The arithmetic mirrors the native <c>ShrapnelMinigame.BreakGrasp</c> read for read, including
	/// its absence of clamping. A clamp here would commit a different number than the one both sides
	/// drew whenever a limb field sits below the draw — which is exactly the divergence the derived
	/// window exists to remove — and the native path itself writes those fields negative.
	/// </para>
	/// </summary>
	internal void ApplyBreakGrasp(ShrapnelOperationSession shrapnel, ShrapnelGripSlipValues values)
	{
		var targetData = _access.GetCharacterData(shrapnel.Target);
		if (targetData is null || shrapnel.LimbIndex < 0 || shrapnel.LimbIndex >= targetData.Limbs.Count)
		{
			return;
		}

		var newData = PlayerCharacterAccess.CloneCharacter(targetData);
		var limb = newData.Limbs[shrapnel.LimbIndex];
		limb.SkinHealth -= values.Skin;
		limb.BleedAmount += values.Bleed;
		limb.Pain += values.Pain;
		if (values.BrainLoss > 0f && limb.IsHead && newData.Health is not null)
		{
			newData.Health.BrainHealth -= values.BrainLoss;
		}

		_access.SaveCharacterData(shrapnel.Target, newData);
		_log.LogWarning("[Shrapnel] session {OperationId} break-grasp applied to {Target} limb {Limb}: skin -{Skin} bleed +{Bleed} pain +{Pain} brain -{Brain}.",
			shrapnel.OperationId, shrapnel.Target, shrapnel.LimbIndex, values.Skin, values.Bleed, values.Pain, values.BrainLoss);
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
		=> BuildState(shrapnel, shrapnel.Operators.FirstOrDefault());

	internal MedicalOperationStateMsg BuildState(ShrapnelOperationSession shrapnel, ulong recipient)
	{
		var targetData = _access.GetCharacterData(shrapnel.Target);
		var isOperator = shrapnel.Operators.Contains(recipient);
		var operatorId = isOperator ? recipient : shrapnel.Operators.FirstOrDefault();

		ulong itemInstanceId = 0;
		CharacterItemMsg? itemAfter = null;
		if (isOperator && shrapnel.OperatorItems.TryGetValue(recipient, out var itemId))
		{
			itemInstanceId = itemId;
			itemAfter = FindItemAfter(recipient, itemId);
		}

		return new MedicalOperationStateMsg
		{
			OperationId = shrapnel.OperationId,
			OperatorSteamId = operatorId,
			TargetSteamId = shrapnel.Target,
			ItemInstanceId = itemInstanceId,
			LimbIndex = shrapnel.LimbIndex,
			Sequence = shrapnel.Sequence,
			ShrapnelPieces = BuildPieces(shrapnel),
			ItemAfter = itemAfter,
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
			Kind = MedicalOperationKind.Shrapnel,
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

	private CharacterItemMsg? FindItemAfter(ulong steamId, ulong itemInstanceId)
	{
		var data = _access.GetCharacterData(steamId);
		if (data is null)
		{
			return null;
		}

		var index = PlayerItemIndex.Find(data, itemInstanceId);
		return index < 0 || index >= data.Items.Count ? null : PlayerCharacterAccess.CloneItem(data.Items[index]);
	}

}
