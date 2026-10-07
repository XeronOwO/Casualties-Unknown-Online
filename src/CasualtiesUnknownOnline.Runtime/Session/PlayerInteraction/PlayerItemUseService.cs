using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cross-player item-use operation (drink/food first slice plus the
/// curated medicine, limb-tool and wearable slices and the migrated topical
/// one). The host validates the user and target against its authoritative
/// character snapshots, consumes/drains a carried item or transfers a wearable
/// onto the target's snapshot, applies the curated target-side body/limb effect
/// and sends the two participants one authoritative result. It has no mutable
/// session state — it only reacts to calls and messages.
/// <para>
/// The topical family is the exception the migration created: the host commits
/// only the resource and carries the operator-reported dose to the target, whose
/// own client runs the liquids' native <c>onHealthUse</c> bodies. The host
/// therefore writes no target state for it, and the result carries no
/// host-computed body snapshot — see
/// <c>mod-cross-player-native-semantics</c> Part B.
/// </para>
/// </summary>
internal sealed class PlayerItemUseService(
	ISessionControl session,
	PacketSender sender,
	PlayerCharacterAccess characters,
	IItemControl items,
	IPlayerInteractionVisibility visibility,
	ILimbUseSemantics limbUseSemantics,
	ItemKernelAuthority kernelAuthority,
	PlayerInteractionResultAuthority resultAuthority,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly IItemControl _items = items;
	private readonly IPlayerInteractionVisibility _visibility = visibility;
	private readonly ILimbUseSemantics _limbUseSemantics = limbUseSemantics;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly PlayerInteractionResultAuthority _resultAuthority = resultAuthority;
	private readonly ILogger _log = log;

	/// <summary>An authoritative cross-player consumable use result arrived — the Game Adapter applies the local participant half.</summary>
	public event Action<PlayerItemUseResultMsg>? UseReceived;

	/// <summary>Online UI entry: the local player uses one carried non-injectable consumable on another player (0 = host auto-select).</summary>
	public void SendUseRequest(ulong targetSteamId, ulong itemInstanceId = 0, int targetLimbIndex = -1, float doseMl = 0f)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (!_visibility.HasLineOfSight(_session.LocalSteamId, targetSteamId))
		{
			_log.LogInformation("[ItemUse] refused locally: {User} cannot see {Target} on this client.", _session.LocalSteamId, targetSteamId);
			return;
		}

		var msg = new PlayerItemUseRequestMsg
		{
			TargetSteamId = targetSteamId,
			ItemInstanceId = itemInstanceId,
			LimbIndex = targetLimbIndex,
			DoseMl = doseMl,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleUseRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerItemUseRequest, msg);
		}
	}

	/// <summary>Host only: a use request arrived — the guest→host wire and the host's own UI share this path.</summary>
	public void HandleUseRequest(ulong sender, PlayerItemUseRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var user = sender;
		var target = msg.TargetSteamId;
		if (user == target || user == 0 || target == 0)
		{
			return;
		}

		TryExecuteUse(user, target, msg.ItemInstanceId, msg.LimbIndex, msg.DoseMl, requireUserConscious: true);
	}

	/// <summary>
	/// Host-authoritative use of a remote player's carried item on the
	/// requesting player's own body. This is the "held-remote-item + Tab close +
	/// R medical use" family: the item owner retains ownership (and their local
	/// item state is updated/consumed by the authoritative use result), while the
	/// requester receives the body-side effect. The item owner does not have to
	/// be conscious — the requester is the active user; the requester must be
	/// conscious/alive and able to see the owner.
	/// </summary>
	internal void HandleRemoteHeldItemUse(
		ulong requester,
		ulong itemOwner,
		ulong itemInstanceId,
		int targetLimbIndex,
		float doseMl)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (requester == 0 || itemOwner == 0 || itemOwner == requester)
		{
			_log.LogWarning("[HeldItemUse] refused invalid requester/owner pair ({Requester} -> {Owner}).", requester, itemOwner);
			return;
		}

		if (!_characters.IsInWorld(requester) || !_characters.IsInWorld(itemOwner))
		{
			_log.LogWarning("[HeldItemUse] refused: requester {Requester} or owner {Owner} is not in-world.", requester, itemOwner);
			return;
		}

		// The requester is the person physically using the item on their own
		// body, so they must be conscious/alive. The owner is only the item
		// source and may be unconscious/dead (the common remote-backpack case).
		if (_characters.GetCharacterData(requester)?.Health is not { } requesterHealth
			|| !requesterHealth.Conscious
			|| !requesterHealth.Alive)
		{
			_log.LogInformation("[HeldItemUse] refused: requester {Requester} is not conscious/alive.", requester);
			return;
		}

		TryExecuteUse(itemOwner, requester, itemInstanceId, targetLimbIndex, doseMl, requireUserConscious: false);
	}

	/// <summary>
	/// Shared cross-player use execution. The kernel authoritative item is
	/// consumed/updated on <paramref name="user"/>'s character data, the body
	/// effect is applied to <paramref name="target"/>'s character data, and the
	/// result event is published for both participants. The item lookup and
	/// replacement are recursive so container-nested remote items (trash bags,
	/// backpacks) work exactly like direct slots.
	/// </summary>
	private bool TryExecuteUse(
		ulong user,
		ulong target,
		ulong itemInstanceId,
		int limbIndex,
		float doseMl,
		bool requireUserConscious)
	{
		if (!_characters.IsInWorld(user) || !_characters.IsInWorld(target))
		{
			_log.LogWarning("[ItemUse] refused: {User} or {Target} is not in-world.", user, target);
			return false;
		}

		var userData = _characters.GetCharacterData(user);
		var targetData = _characters.GetCharacterData(target);
		if (userData is null || targetData is null)
		{
			_log.LogWarning("[ItemUse] refused: no character snapshot for {User}/{Target}.", user, target);
			return false;
		}

		if (requireUserConscious
			&& (userData.Health is not { } userHealth || !userHealth.Conscious || !userHealth.Alive))
		{
			_log.LogInformation("[ItemUse] refused: {User} is not conscious/alive and cannot use an item.", user);
			return false;
		}

		if (targetData.Health is not { } targetHealth || !targetHealth.Conscious || !targetHealth.Alive)
		{
			_log.LogInformation("[ItemUse] refused: {Target} is not conscious/alive and cannot receive a consumable.", target);
			return false;
		}

		CharacterItemMsg? originalItem;
		if (itemInstanceId != 0)
		{
			if (!CarriedItemUseTree.TryFind(userData.Items, itemInstanceId, out originalItem))
			{
				_log.LogWarning("[ItemUse] refused: {User} has no usable consumable (requested {ItemId}).", user, itemInstanceId);
				return false;
			}
		}
		else
		{
			originalItem = CarriedItemUseTree.FindFirstUsable(userData.Items, _limbUseSemantics);
			if (originalItem is null)
			{
				_log.LogWarning("[ItemUse] refused: {User} has no usable consumable to auto-select.", user);
				return false;
			}
		}

		if (!CarriedItemUseTree.IsActuallyUsable(originalItem, _limbUseSemantics))
		{
			_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) is empty or not in the catalog.", originalItem.ItemId, originalItem.InstanceId);
			return false;
		}

		var newUserData = PlayerCharacterAccess.CloneCharacter(userData);
		var newItem = PlayerCharacterAccess.CloneItem(originalItem);
		var newTargetData = PlayerCharacterAccess.CloneCharacter(targetData);
		var destroyed = false;
		CharacterItemMsg? wornItem = null;
		List<LiquidStackMsg>? appliedDose = null;
		var timedEffects = new List<TimedLimbEffectMsg>();
		var timedBodyEffects = new List<TimedBodyEffectMsg>();

		if (RemoteWearCatalog.IsWearItem(originalItem.ItemId))
		{
			if (!RemoteWearApplication.TryCreateWornItem(newTargetData.Limbs, newTargetData.Items, originalItem, out wornItem))
			{
				_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) cannot be placed on {Target} — target limb missing/dismembered or wear slot already occupied.", originalItem.ItemId, originalItem.InstanceId, target);
				return false;
			}

			newTargetData.Items.Add(wornItem);
			destroyed = true; // the acting player's local item is removed; the wire carries WornItem for the target side
			_log.LogInformation("[ItemUse] {User} wears {ItemId} (id {InstanceId}) on {Target}; slot {Slot}.", user, originalItem.ItemId, originalItem.InstanceId, target, wornItem.SlotIndex);
		}
		else if (RemoteConsumeApplication.TryCreateDrinkPlan(originalItem.Liquids, out var drinkPlan))
		{
			RemoteConsumeApplication.ApplyDrink(newTargetData.Health!, drinkPlan);
			CarriedItemUseTree.ApplyDrain(newItem, drinkPlan);
		}
		else if (RemoteConsumeCatalog.TryGetFood(originalItem.ItemId, out var food))
		{
			RemoteConsumeApplication.ApplyFood(newTargetData.Health!, food);
			newItem.Condition -= food.ConditionCost;
			destroyed = newItem.Condition <= 0f;
		}
		else if (InjectionAdmission.IsInjectableContainer(_limbUseSemantics, originalItem.ItemId, originalItem.Liquids))
		{
			_log.LogWarning("[ItemUse] refused: injectable/IV medicine {ItemId} (id {InstanceId}) must use the medical operation session, not the one-shot request path.",
				originalItem.ItemId, originalItem.InstanceId);
			return false;
		}
		else if (RemoteDrinkMedicineCatalog.TryCreatePlan(originalItem.Liquids, originalItem.ItemId, out var drinkMedicinePlan))
		{
			if (RemoteDrinkMedicineCatalog.IsMindwipeBlocked(originalItem.ItemId, newTargetData.Health!))
			{
				_log.LogInformation("[ItemUse] refused: {Target} is still mentally healthy for mindwipe.", target);
				return false;
			}

			RemoteDrinkMedicineApplication.Apply(newTargetData.Health!, drinkMedicinePlan);
			timedBodyEffects = RemoteDrinkMedicineApplication.BuildTimedEffects(drinkMedicinePlan);
			CarriedItemUseTree.ApplyDrain(newItem, drinkMedicinePlan);
		}
		else if (TopicalAdmission.IsTopicalContainer(_limbUseSemantics, originalItem.ItemId, originalItem.Liquids))
		{
			// The dose is the one the item's OWN delegate computed on the
			// operator's client — the per-use ml is an ldc.r4 literal inside that
			// closure, so no table can hold it and the host only caps it at what
			// the item really carries (LiquidDrainPlan mirrors the native
			// CalculateDrain). The effect belongs to the affected side: this
			// branch commits the resource and carries the drained plan to the
			// target, which runs each liquid's own onHealthUse through the native
			// path. Nothing target-side is computed or saved here.
			if (!LiquidDrainPlan.TryCreate(originalItem.Liquids, doseMl, out var topicalPlan))
			{
				_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) carried no dose to apply to {Target}.", originalItem.ItemId, originalItem.InstanceId, target);
				return false;
			}

			appliedDose = topicalPlan;
			CarriedItemUseTree.ApplyDrain(newItem, topicalPlan);
		}
		else if (RemoteLimbToolCatalog.TryGet(originalItem.ItemId, out var tool))
		{
			if (!RemoteLimbToolApplication.TryApply(
				newTargetData.Health!,
				newTargetData.Limbs,
				tool,
				out var resolvedLimbIndex,
				limbIndex,
				originalItem.Condition))
			{
				_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) cannot be applied to {Target} — required limb missing, no limb data, or component ineligible.", originalItem.ItemId, originalItem.InstanceId, target);
				return false;
			}

			newItem.Condition -= tool.ConditionCost;
			destroyed = newItem.Condition <= 0f && tool.DestroyAtZero;
			if (tool.TimedBleedDurationSeconds > 0f)
			{
				timedEffects.Add(new TimedLimbEffectMsg
				{
					LimbIndex = resolvedLimbIndex,
					DurationSeconds = tool.TimedBleedDurationSeconds,
					BleedPerSecond = tool.TimedBleedPerSecond,
				});
			}
		}
		else
		{
			_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) is not in the remote-consumable/medicine/topical/limb-tool catalog.", originalItem.ItemId, originalItem.InstanceId);
			return false;
		}

		if (destroyed)
		{
			CarriedItemUseTree.Remove(newUserData.Items, originalItem.InstanceId);
		}
		else
		{
			CarriedItemUseTree.Replace(newUserData.Items, originalItem.InstanceId, newItem);
		}

		_characters.SaveCharacterData(user, newUserData);
		if (appliedDose is null)
		{
			_characters.SaveCharacterData(target, newTargetData);
		}

		if (user != _session.LocalSteamId)
		{
			if (destroyed)
			{
				_items.RemoveTransferredItem(user, originalItem.InstanceId);
				if (wornItem is null)
				{
					DestroyKernelCarriedItemIfPresent(originalItem.InstanceId);
				}
			}
			else
			{
				_items.UpdateTransferredItem(user, originalItem.InstanceId, PlayerCharacterAccess.CloneItem(newItem));
			}
		}
		else
		{
			SyncHostItemAfterUse(originalItem.InstanceId, newItem, destroyed);
		}

		// A wearable transfer moves the item into the target's ownership. For a
		// guest target the transfer table must learn the item so reconnect
		// restore and arbitration see it as the target's own carried fact.
		if (wornItem is not null)
		{
			if (target != _session.LocalSteamId)
			{
				_items.AdoptTransferredItem(target, originalItem.InstanceId, PlayerCharacterAccess.CloneItem(wornItem));
			}
			else
			{
				CommitWornItemToHost(originalItem.InstanceId, wornItem);
			}
		}

		_log.LogInformation(
			"[ItemUse] {User} used {ItemId} (id {InstanceId}) on {Target}; destroyed={Destroyed}.",
			user, originalItem.ItemId, originalItem.InstanceId, target, destroyed);

		// The migrated topical family publishes no host-computed body state: the
		// target applies the dose itself through the native path, and neither
		// display sink has a staleness guard, so echoing the target's own
		// pre-dose report back would fight the effect this same message carries.
		// Every other family keeps the host-applied snapshot untouched.
		var targetAppliesLocally = appliedDose is not null;
		PublishUse(new PlayerItemUseResultMsg
		{
			UserSteamId = user,
			TargetSteamId = target,
			ItemInstanceId = originalItem.InstanceId,
			ItemDestroyed = destroyed,
			ItemAfter = destroyed ? null : PlayerCharacterAccess.CloneItem(newItem),
			WornItem = wornItem,
			Health = targetAppliesLocally ? null : newTargetData.Health,
			Limbs = targetAppliesLocally ? [] : [.. newTargetData.Limbs],
			TimedEffects = timedEffects,
			TimedBodyEffects = timedBodyEffects,
			AppliedDose = appliedDose ?? [],
			LimbIndex = limbIndex,
		});
		return true;
	}

	/// <summary>Kernel projection path: a use result event arrived — surface it for the Game Adapter.</summary>
	public void FireUseReceived(PlayerItemUseResultMsg msg) => UseReceived?.Invoke(msg);

	/// <summary>Remove a guest-owned item from the kernel when a cross-player use consumed it (non-wearable).</summary>
	private void DestroyKernelCarriedItemIfPresent(ulong itemId)
	{
		if (_kernelAuthority.FindItem(itemId) is not null)
		{
			_kernelAuthority.TryDestroy(_session.LocalSteamId, itemId, TerminalKind.Consumed, out _, out _);
		}
	}

	/// <summary>
	/// Keep the item kernel authoritative when the host is the user: a destroyed
	/// host item is removed from the kernel when known; a surviving host item is
	/// spawned/updated with the post-use state.
	/// </summary>
	private void SyncHostItemAfterUse(ulong itemId, CharacterItemMsg item, bool destroyed)
	{
		var current = _kernelAuthority.FindItem(itemId);
		if (destroyed)
		{
			if (current is not null)
			{
				_kernelAuthority.TryDestroy(_session.LocalSteamId, itemId, TerminalKind.Consumed, out _, out _);
			}

			return;
		}

		if (current is null)
		{
			_kernelAuthority.TrySpawnCarried(_session.LocalSteamId, itemId, item.ItemId, item, out _, out _);
		}
		else
		{
			_kernelAuthority.TryUpdateState(_session.LocalSteamId, itemId, item, out _, out _);
		}
	}

	/// <summary>
	/// Make the kernel own a wearable that crossed to the host target. Guest
	/// targets go through the transfer-table adopt path; the host has no
	/// transfer-table row, so this keeps the item kernel authoritative when the
	/// host becomes the owner.
	/// </summary>
	private void CommitWornItemToHost(ulong itemId, CharacterItemMsg item)
	{
		var current = _kernelAuthority.FindItem(itemId);
		if (current is null)
		{
			_kernelAuthority.TrySpawnCarried(_session.LocalSteamId, itemId, item.ItemId, item, out _, out _);
			return;
		}

		_kernelAuthority.TryTransfer(
			_session.LocalSteamId,
			itemId,
			new ActorId(_session.LocalSteamId),
			item,
			out _,
			out _);
	}

	private void PublishUse(PlayerItemUseResultMsg msg)
	{
		// The kernel is the single authority. The committed result event is
		// broadcast through KernelEnvelope and the PlayerInteractionKernelProjection
		// raises this same event on the host (BatchCommitted) and guests
		// (BatchApplied); no legacy direct result wire remains.
		if (!_resultAuthority.TryRecordPlayerItemUseResult(
			_session.LocalSteamId,
			msg.UserSteamId,
			msg.TargetSteamId,
			msg.ItemInstanceId,
			msg.ItemDestroyed,
			msg.ItemAfter is null ? null : PlayerInteractionKernelCodec.FromCharacterItem(msg.ItemAfter),
			msg.WornItem is null ? null : PlayerInteractionKernelCodec.FromCharacterItem(msg.WornItem),
			msg.Health is null ? null : PlayerInteractionKernelCodec.FromCharacterHealth(msg.Health),
			[.. msg.Limbs.Select(PlayerInteractionKernelCodec.FromCharacterLimb)],
			[.. msg.TimedEffects.Select(PlayerInteractionKernelCodec.FromTimedLimbEffect)],
			[.. msg.TimedBodyEffects.Select(PlayerInteractionKernelCodec.FromTimedBodyEffect)],
			[.. msg.AppliedDose.Select(PlayerInteractionKernelCodec.FromLiquidStack)],
			msg.LimbIndex,
			out _,
			out var rejection))
		{
			_log.LogWarning("[ItemUse] kernel result rejected {User} -> {Target}: {Reason} ({Message}).",
				msg.UserSteamId, msg.TargetSteamId, rejection!.Reason, rejection.Message);
		}
	}
}
