using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Host-side removal effects for splint and tourniquet components. It clears
/// the target limb component and lower-limb bleed block, and MOVES the item that
/// was installed there into the operator's hands.
/// <para>
/// The move is the ruling this family implements: the applied row on that limb is
/// the item's own record, so the removal relocates it (applied → carried by the
/// operator) and the instance id is unchanged. Nothing is minted, and the limb row's
/// component template is only what NAMES the item — the id comes from the row.
/// </para>
/// <para>
/// A limb component with no applied row is a DIVERGENCE, and it is refused rather
/// than papered over: the item row is the authority for which item is installed, so
/// a component that no row names has no id to move, and inventing one is exactly the
/// synthesized namespace this family deleted. The refusal happens before anything is
/// cleared, so the two carriers are left as they were found rather than half-moved.
/// </para>
/// </summary>
internal sealed class OtherMedicalRemovalApplier(
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

		// WHICH item this component is, asked of the one carrier that records it: the
		// applied row on this limb whose definition is the component's own template (a
		// splint and a tourniquet can share one limb, and this is what tells them apart).
		// Resolved BEFORE anything is cleared — the removal is a move of that row, and an
		// operation that cannot name its item must change nothing at all.
		var applied = _kernelAuthority.FindAppliedLimbItem(new ActorId(session.Target), session.LimbIndex, itemId);
		if (applied is null)
		{
			_log.LogError(
				"[MedicalOps] refused the {Kind} on limb {Limb} of {Target}: the limb carries a component whose template is {ItemId}, but no applied item row names that limb and definition — the item's id is unknown, so nothing was moved and the component stays. This is the two-carriers divergence the applied family reports rather than guesses at.",
				session.Kind, session.LimbIndex, session.Target, itemId);
			return false;
		}

		// The hand-over is checked before anything moves, so every refusal below leaves
		// the component, the row and the operator exactly as they were found.
		var newOperator = PlayerCharacterAccess.CloneCharacter(operatorData);
		var slot = PlayerCharacterAccess.FirstEmptySlot(newOperator);
		if (slot < 0)
		{
			_log.LogWarning("[MedicalOps] cannot hand removed {ItemId} (id {InstanceId}) to {Operator}: no empty inventory slot.",
				itemId, applied.Value.Identity.InstanceId, session.Operator);
			return false;
		}

		var itemInstanceId = applied.Value.Identity.InstanceId;
		if (!_kernelAuthority.TryRemoveLimb(
			_session.LocalSteamId,
			itemInstanceId,
			new ActorId(session.Target),
			session.LimbIndex,
			new ActorId(session.Operator),
			null,
			out _,
			out var rejection))
		{
			_log.LogError(
				"[MedicalOps] the kernel refused to move applied {ItemId} (id {InstanceId}) off limb {Limb} of {Target} to {Operator}: {Reason} ({Message}) — the component stays, nothing was handed over.",
				itemId, itemInstanceId, session.LimbIndex, session.Target, session.Operator,
				rejection?.Reason, rejection?.Message);
			return false;
		}

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

		// The operator's snapshot gains the SAME id the row carried while installed, and a
		// guest operator's transfer table records it so a reconnect merge and the next
		// arbitration see it as that guest's own carried fact. The kernel is not asked
		// again: the relocation above already put the row in the operator's hands, and a
		// second transfer to the same owner would be a second move for one gesture.
		var handed = new CharacterItemMsg
		{
			InstanceId = itemInstanceId,
			ItemId = itemId,
			Condition = condition,
			SlotIndex = slot,
		};
		newOperator.Items.Add(handed);
		session.AwardedItemId = itemInstanceId;
		_access.SaveCharacterData(session.Operator, newOperator);
		if (session.Operator != _session.LocalSteamId)
		{
			_items.RegisterTransferredItem(session.Operator, itemInstanceId, PlayerCharacterAccess.CloneItem(handed));
		}

		_log.LogInformation("[MedicalOps] handed removed {ItemId} (id {InstanceId}) to {Operator} in slot {Slot} — the item kept its id across the round trip.",
			itemId, itemInstanceId, session.Operator, slot);
		return true;
	}

	/// <summary>
	/// Native TourniquetScript.GetLowerLimbs walks the connected graph and keeps
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
