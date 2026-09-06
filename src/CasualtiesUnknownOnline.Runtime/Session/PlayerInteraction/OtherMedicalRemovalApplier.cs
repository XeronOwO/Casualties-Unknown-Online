using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Host-side removal effects for splint and tourniquet components. It clears
/// the target limb component and lower-limb bleed block, creates the removed
/// item as a host-synthesized carried item and hands it to the operator.
/// </summary>
internal sealed class OtherMedicalRemovalApplier(
	PlayerCharacterAccess access,
	IItemControl items,
	ItemKernelAuthority kernelAuthority,
	ISessionControl session,
	ILogger log)
{
	private const ulong AwardedItemHighBit = 1UL << 63;

	private readonly PlayerCharacterAccess _access = access;
	private readonly IItemControl _items = items;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ISessionControl _session = session;
	private readonly ILogger _log = log;
	private ulong _nextAwardedItemId;

	internal bool Remove(OtherMedicalOperationSession session)
	{
		var targetData = _access.GetCharacterData(session.Target);
		var operatorData = _access.GetCharacterData(session.Operator);
		if (targetData is null || operatorData is null || session.LimbIndex < 0 || session.LimbIndex >= targetData.Limbs.Count)
		{
			return false;
		}

		var newTarget = PlayerCharacterAccess.CloneCharacter(targetData);
		var limb = newTarget.Limbs[session.LimbIndex];
		var componentType = session.Kind == MedicalOperationKind.SplintRemoval ? "SplintLimb" : "TourniquetScript";
		var component = limb.Components.FirstOrDefault(c => c.TypeName == componentType);
		if (component is null)
		{
			return false;
		}

		var itemId = componentType == "SplintLimb"
			? GetStringField(component, "item") ?? "splint"
			: "tourniquet";
		var condition = GetFloatField(component, "condition");

		limb.Components.RemoveAll(c => c.TypeName == componentType);
		if (componentType == "SplintLimb")
		{
			limb.Splinted = false;
		}
		else
		{
			limb.BlockedBleeding = false;
			ClearLowerTourniquetBlock(newTarget, limb);
		}

		_access.SaveCharacterData(session.Target, newTarget);
		GrantRemovedItemToOperator(session, operatorData, itemId, condition);
		return true;
	}

	// Native TourniquetScript.GetLowerLimbs walks the connected graph and keeps
	// only limbs farther from the heart. The wire snapshot now carries that
	// graph, so the host can clear the same set without running Unity.
	private void ClearLowerTourniquetBlock(CharacterDataMsg data, CharacterLimbMsg root) =>
		VisitLower(data, root, root.Index, []);

	private void VisitLower(CharacterDataMsg data, CharacterLimbMsg current, int rootIndex, HashSet<int> visited)
	{
		foreach (var index in current.ConnectedLimbIndices)
		{
			if (index < 0 || index >= data.Limbs.Count || !visited.Add(index))
			{
				continue;
			}

			var limb = data.Limbs[index];
			if (index == rootIndex)
			{
				continue;
			}

			if (limb.DistanceToHeart > current.DistanceToHeart)
			{
				limb.BlockedBleeding = false;
				VisitLower(data, limb, rootIndex, visited);
			}
		}
	}

	private void GrantRemovedItemToOperator(OtherMedicalOperationSession session, CharacterDataMsg operatorData, string itemId, float condition)
	{
		var newOperator = PlayerCharacterAccess.CloneCharacter(operatorData);
		var slot = PlayerCharacterAccess.FirstEmptySlot(newOperator);
		if (slot < 0)
		{
			_log.LogWarning("[MedicalOps] cannot hand removed {ItemId} to {Operator}: no empty inventory slot.", itemId, session.Operator);
			return;
		}

		var awarded = new CharacterItemMsg
		{
			InstanceId = NextAwardedItemId(),
			ItemId = itemId,
			Condition = condition,
			SlotIndex = slot,
		};
		newOperator.Items.Add(awarded);
		session.AwardedItemId = awarded.InstanceId;
		_access.SaveCharacterData(session.Operator, newOperator);
		SyncOperatorAwardedItem(session.Operator, awarded);
		_log.LogInformation("[MedicalOps] awarded removed {ItemId} (id {InstanceId}) to {Operator} in slot {Slot}.",
			itemId, awarded.InstanceId, session.Operator, slot);
	}

	// Reserved high-bit namespace for host-synthesized removed components.
	// Normal item ids use (counter, account-low) and never set bit 63, so this
	// cannot collide with guest/host-generated items in this session.
	private ulong NextAwardedItemId() =>
		AwardedItemHighBit | (_nextAwardedItemId++ << 32) | ((uint)_session.LocalSteamId & 0xFFFFFFFF);

	private void SyncOperatorAwardedItem(ulong operatorId, CharacterItemMsg item)
	{
		if (operatorId == _session.LocalSteamId)
		{
			_kernelAuthority.TrySpawnCarried(_session.LocalSteamId, item.InstanceId, item.ItemId, item, out _, out _);
		}
		else
		{
			_items.AdoptTransferredItem(operatorId, item.InstanceId, PlayerCharacterAccess.CloneItem(item));
		}
	}

	private static float GetFloatField(ComponentStateMsg component, string name)
	{
		foreach (var field in component.Fields)
		{
			if (field.Name == name && field.Kind == SaveableFieldKind.Float)
			{
				return field.FloatValue;
			}
		}

		return 0f;
	}

	private static string? GetStringField(ComponentStateMsg component, string name)
	{
		foreach (var field in component.Fields)
		{
			if (field.Name == name && field.Kind == SaveableFieldKind.String)
			{
				return field.StringValue;
			}
		}

		return null;
	}
}
