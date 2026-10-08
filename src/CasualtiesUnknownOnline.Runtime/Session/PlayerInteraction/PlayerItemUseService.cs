using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cross-player item-use operation (the wearable slice and the five migrated
/// native families: injection, topical, drink, solid food and the limb tool). The
/// host validates the user and target against its authoritative character
/// snapshots, commits the resource a use spends — the drain a topical or drink
/// gesture measured, the state and the consumption a limb tool's own delegate left
/// on the affected side — and sends the two participants one authoritative result.
/// It has no mutable session state — it only reacts to calls and messages.
/// <para>
/// Which family a carried item belongs to is the item's own data, asked through
/// the four content seams (<see cref="ILimbUseSemantics"/>,
/// <see cref="IConsumeSemantics"/>, <see cref="IWearSemantics"/>,
/// <see cref="ISolidFoodSemantics"/>) in the order the gesture routing uses, so no
/// CUO id table decides it. The limb tool's family is the one whose item-level
/// claims are still asked as well (<see cref="LimbToolAdmission"/>), because the
/// session chains that own the minigame families have not migrated yet.
/// </para>
/// <para>
/// The migrated families are the exception the migration created: the host
/// commits only the resource and carries the operator-measured dose to the
/// target, whose own client runs the game's own code — the liquids'
/// <c>onHealthUse</c> for a limb application, their <c>onDrink</c> for a drink.
/// The host therefore writes no target state for them, and the result carries no
/// host-computed body snapshot — see <c>mod-cross-player-native-semantics</c>
/// Parts A and B.
/// </para>
/// <para>
/// The two families whose effect belongs ENTIRELY to the affected side are the
/// migration's end point: nothing is measured and nothing is computed anywhere but
/// there — the LIMB TOOL runs the item's own <c>useLimbAction</c> against the
/// treated player's own limb (<see cref="LimbToolAdmission"/>), and the SOLID FOOD
/// runs the item's own <c>Body.UseItem</c> → <c>useAction</c> against the eater's
/// own body. The host's part of either request is the ADMISSION (the grant the
/// affected side's outcome report is matched against) plus the second result it
/// publishes from that report, so the item — which never changed owner — reaches
/// its owner's own item. Where a use's item state LANDS is
/// <see cref="PlayerItemUseCommit"/>'s.
/// </para>
/// </summary>
internal sealed class PlayerItemUseService : ISessionReset, IDisposable
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly PlayerCharacterAccess _characters;
	private readonly IItemControl _items;
	private readonly IPlayerInteractionVisibility _visibility;
	private readonly ILimbUseSemantics _limbUseSemantics;
	private readonly IConsumeSemantics _consumeSemantics;
	private readonly IWearSemantics _wearSemantics;
	private readonly ISolidFoodSemantics _solidFoodSemantics;
	private readonly PlayerInteractionResultAuthority _resultAuthority;
	private readonly PlayerItemUseCommit _commit;
	private readonly PlayerItemActionOutcomeService _outcomes;
	private readonly ILogger _log;

	public PlayerItemUseService(
		ISessionControl session,
		PacketSender sender,
		PlayerCharacterAccess characters,
		IItemControl items,
		IPlayerInteractionVisibility visibility,
		ILimbUseSemantics limbUseSemantics,
		IConsumeSemantics consumeSemantics,
		IWearSemantics wearSemantics,
		ISolidFoodSemantics solidFoodSemantics,
		ItemKernelAuthority kernelAuthority,
		PlayerInteractionResultAuthority resultAuthority,
		ILogger log)
	{
		_session = session;
		_sender = sender;
		_characters = characters;
		_items = items;
		_visibility = visibility;
		_limbUseSemantics = limbUseSemantics;
		_consumeSemantics = consumeSemantics;
		_wearSemantics = wearSemantics;
		_solidFoodSemantics = solidFoodSemantics;
		_resultAuthority = resultAuthority;
		_log = log;

		// One commit side and one affected-side outcome half, shared with the family
		// chain below: the state a use leaves on the item lives in exactly one place
		// (statement over the same objects twice would be two answers to "who
		// committed this"), and both families whose effect runs on the affected side
		// report through the same admission table.
		_commit = new PlayerItemUseCommit(session, characters, items, kernelAuthority, log);
		_outcomes = new PlayerItemActionOutcomeService(characters, limbUseSemantics, solidFoodSemantics, _commit, log);
	}

	/// <summary>Composition wiring: the affected-side admission table is session-scoped, so the session's end drops it.</summary>
	internal void BindToSession() => _session.SessionEnded += ResetSessionState;

	public void Dispose() => _session.SessionEnded -= ResetSessionState;

	public void ResetSessionState() => _outcomes.ResetSessionState();

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
			originalItem = CarriedItemUseTree.FindFirstUsable(userData.Items, _limbUseSemantics, _consumeSemantics, _wearSemantics, _solidFoodSemantics);
			if (originalItem is null)
			{
				_log.LogWarning("[ItemUse] refused: {User} has no usable consumable to auto-select.", user);
				return false;
			}
		}

		if (!CarriedItemUseTree.IsActuallyUsable(originalItem, _limbUseSemantics, _consumeSemantics, _wearSemantics, _solidFoodSemantics))
		{
			// The solid-food family's one refused shape keeps its own reason: the item
			// does feed a body, but its own use action would hand the eater a
			// replacement object, and there is nothing on the affected side to hand it
			// to — the object would be created in the EATER's world (a phantom item at
			// its parked position, plus the game's own "too far" alert when it tries to
			// put it in the eater's hand). Asked before the family chain so the refusal
			// is never reported as "not a family this path carries".
			if (SolidFoodAdmission.Classify(_solidFoodSemantics, originalItem.ItemId) == SolidFoodVerdict.EatsAndReplaces)
			{
				_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) feeds a body but hands the eater a replacement object when its own use action runs — the cross-player path cannot deliver that object.", originalItem.ItemId, originalItem.InstanceId);
				return false;
			}

			_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) is empty or is not a family the one-shot path carries.", originalItem.ItemId, originalItem.InstanceId);
			return false;
		}

		var newItem = PlayerCharacterAccess.CloneItem(originalItem);
		var newTargetData = PlayerCharacterAccess.CloneCharacter(targetData);
		var destroyed = false;
		CharacterItemMsg? wornItem = null;
		List<LiquidStackMsg>? appliedDose = null;
		List<LiquidStackMsg>? drinkDose = null;

		// Where the item goes is the item's OWN data (ItemInfo.wearable /
		// desiredWearLimb / wearSlotId), read through the same seam the operator's
		// gesture asks, so the deleted 40-row id table no longer decides which
		// wearables exist. One seam call answers both halves of the question, so a
		// wearable can never be admitted and then fail to resolve: the read is the
		// admission. The limb index comes back resolved because the character
		// snapshot encodes a worn item as -(index + 2); the game's occupancy check
		// compares the wear SLOT id, which is what makes two wearables in one slot
		// collide even when they name different limbs.
		if (_wearSemantics.TryGetWearPlacement(originalItem.ItemId, out var wearLimbIndex, out var wearSlotId))
		{
			if (!RemoteWearApplication.TryCreateWornItem(_wearSemantics, wearLimbIndex, wearSlotId, newTargetData.Limbs, newTargetData.Items, originalItem, out wornItem))
			{
				_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) cannot be placed on {Target} — target limb missing/dismembered or wear slot already occupied.", originalItem.ItemId, originalItem.InstanceId, target);
				return false;
			}

			newTargetData.Items.Add(wornItem);
			destroyed = true; // the acting player's local item is removed; the wire carries WornItem for the target side
			_log.LogInformation("[ItemUse] {User} wears {ItemId} (id {InstanceId}) on {Target}; slot {Slot}.", user, originalItem.ItemId, originalItem.InstanceId, target, wornItem.SlotIndex);
		}
		else if (InjectionAdmission.IsInjectableContainer(_limbUseSemantics, originalItem.ItemId, originalItem.Liquids))
		{
			// Asked before the drink rule on purpose: the items both rules admit
			// are the liquid containers the game can also draw from a limb
			// (saline, ringersolution, a blood bag), and for those the one-shot
			// path has always refused by name and pointed at the medical
			// operation session. The limb rules ask their own flag
			// (usableOnLimb) while this chain asks `usable`, so the order — not
			// the data — is what keeps a dual-class container on the family the
			// gesture was made for.
			_log.LogWarning("[ItemUse] refused: injectable/IV medicine {ItemId} (id {InstanceId}) must use the medical operation session, not the one-shot request path.",
				originalItem.ItemId, originalItem.InstanceId);
			return false;
		}
		else if (TopicalAdmission.IsTopicalContainer(_limbUseSemantics, originalItem.ItemId, originalItem.Liquids))
		{
			// The decision and the plan live in TopicalUseBranch: the request must name
			// the limb the wound view carried (that release IS the application's native
			// call site, decision 246) and must carry the dose the operator's own item
			// delegate measured. Nothing target-side is computed or saved here.
			if (!TopicalUseBranch.TryPlan(limbIndex, target, originalItem, doseMl, newItem, _log, out appliedDose))
			{
				return false;
			}
		}
		else if (ConsumeAdmission.IsDrinkContainer(_consumeSemantics, originalItem.ItemId, originalItem.Liquids))
		{
			// The dose is the one the item's OWN use action computed on the
			// operator's client — the per-use ml is an ldc.r4 literal inside the
			// delegate that reaches WaterContainerItem.Drink (100 for a water
			// bottle, 20 for naltrexone, 5 for sleeping pills), so no table can
			// hold it and the host only caps it at what the item really carries
			// (LiquidDrainPlan mirrors the native CalculateDrain). The effect
			// belongs to the affected side: this branch commits the resource and
			// carries the drained plan to the target, which runs each liquid's own
			// onDrink delegate. Nothing target-side is computed or saved here.
			//
			// Asked AFTER the limb rules, and in the same order the operator's
			// own entries measure: vanilla never needs the order (every topical
			// carrier is `usable=false` and every drink container that is also
			// limb-usable holds a liquid that is neither injectable nor
			// health-usable), but a mod container the game marks both ways must
			// measure the same native call this chain will run.
			if (!LiquidDrainPlan.TryCreate(originalItem.Liquids, doseMl, out var drinkPlan))
			{
				_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) carried no dose to drink for {Target}.", originalItem.ItemId, originalItem.InstanceId, target);
				return false;
			}

			drinkDose = drinkPlan;
			CarriedItemUseTree.ApplyDrain(newItem, drinkPlan);
		}
		else if (SolidFoodAdmission.Classify(_solidFoodSemantics, originalItem.ItemId) != SolidFoodVerdict.NotSolidFood)
		{
			// The eat belongs to the affected side: its own client runs the game's own
			// Body.UseItem → useAction against its own body and its own object of the
			// offered item, and reports what the item became. NOTHING is committed here
			// — not the item (the eater's own run writes it, and the report below
			// carries the result to its owner) and not the target's body (the game
			// writes it, with its own clamps, rolls, talker reactions and condition
			// cost) — so the host's whole part of the request is the ADMISSION: the
			// grant the eater's one outcome report is matched against, because without
			// it that report would be one member writing another member's item.
			_outcomes.Admit(target, originalItem.InstanceId, user);
			_log.LogInformation("[ItemUse] {User} offers {ItemId} (id {InstanceId}) to {Target} — the eat runs on the eater's own client.",
				user, originalItem.ItemId, originalItem.InstanceId, target);
			PublishUse(new PlayerItemUseResultMsg
			{
				UserSteamId = user,
				TargetSteamId = target,
				ItemInstanceId = originalItem.InstanceId,
				TargetEatsTheItem = true,
			});
			return true;
		}
		else if (LimbToolAdmission.IsLimbTool(_limbUseSemantics, _solidFoodSemantics, originalItem.ItemId))
		{
			// A limb tool has no `useAction` at all — only a `useLimbAction` — so the
			// inventory-use gesture cannot mean it, and the request must say WHICH limb:
			// the medical view's release carries the limb the operator picked, while a
			// world drag releases onto a body and names none. A request without a limb is
			// therefore refused rather than resolved to the affected side's most-injured
			// limb, which would guess at the one thing this action is about.
			if (limbIndex < 0)
			{
				_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) is a limb tool but the request named no limb — it is applied through the medical view, which carries the limb the operator picked.", originalItem.ItemId, originalItem.InstanceId);
				return false;
			}

			// The limb tool belongs to the affected side exactly as the eat above does:
			// its own client runs the item's own useLimbAction against its own limb and
			// its own object of the offered item, so the whole limb effect — the field
			// writes, the limb component the tool turns into (SplintLimb,
			// TourniquetScript, ChilledLimb), the timed op a delegate starts
			// (medicalsuture's bleed ramp), the item condition the delegate itself
			// spends — is the game's own code on the body it lands on. NOTHING is
			// committed here: the host's whole part of the request is the ADMISSION (the
			// grant the affected side's one outcome report is matched against), because
			// without it that report would be one member writing another member's item.
			// The deleted RemoteLimbToolCatalog's transcribed numbers (condition cost,
			// per-tool body deltas, the component fields, the timed ramp) are what this
			// branch no longer carries.
			_outcomes.Admit(target, originalItem.InstanceId, user);
			_log.LogInformation("[ItemUse] {User} uses {ItemId} (id {InstanceId}) on {Target} — the item's own limb action runs on the affected side.",
				user, originalItem.ItemId, originalItem.InstanceId, target);
			PublishUse(new PlayerItemUseResultMsg
			{
				UserSteamId = user,
				TargetSteamId = target,
				ItemInstanceId = originalItem.InstanceId,
				TargetRunsLimbAction = true,
				LimbIndex = limbIndex,
			});
			return true;
		}
		else
		{
			_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) is not in the food/topical/drink/limb-tool families the one-shot path carries.", originalItem.ItemId, originalItem.InstanceId);
			return false;
		}

		// The order is the chain's own and is load-bearing: the owner's snapshot, then the
		// target's, then the item's own record — which is why the commit is asked in two
		// halves here instead of one call (the cross-player eat, which has no target-side
		// state to write, uses the one-call entry).
		_commit.SaveOwnerSnapshot(user, originalItem.InstanceId, newItem, destroyed);
		if (appliedDose is null && drinkDose is null)
		{
			// No target state is written for either migrated family: their effect
			// runs on the affected side, and re-saving the untouched clone here
			// would only claim the host had computed something.
			_characters.SaveCharacterData(target, newTargetData);
		}

		_commit.SyncItemRecord(user, originalItem.InstanceId, newItem, destroyed, movedToTheTarget: wornItem is not null);

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
				_commit.CommitWornItemToHost(originalItem.InstanceId, wornItem);
			}
		}

		_log.LogInformation(
			"[ItemUse] {User} used {ItemId} (id {InstanceId}) on {Target}; destroyed={Destroyed}.",
			user, originalItem.ItemId, originalItem.InstanceId, target, destroyed);

		// The migrated topical and drink families publish no host-computed body
		// state: the target applies the committed dose itself through the game's
		// own code, and neither display sink has a staleness guard, so echoing the
		// target's own pre-dose report back would fight the effect this same
		// message carries. Every other family keeps the host-applied snapshot
		// untouched.
		var targetAppliesLocally = appliedDose is not null || drinkDose is not null;
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
			AppliedDose = appliedDose ?? [],
			DrinkDose = drinkDose ?? [],
			LimbIndex = limbIndex,
		});
		return true;
	}

	/// <summary>Kernel projection path: a use result event arrived — surface it for the Game Adapter.</summary>
	public void FireUseReceived(PlayerItemUseResultMsg msg) => UseReceived?.Invoke(msg);

	/// <summary>
	/// Any role: the local client ran a cross-player use whose effect belongs to this
	/// side — report what the used item became so the host can hand it to the item's
	/// owner. Guest → host on the wire; the host handles its own case locally,
	/// because the host is the affected side there just as a guest is (its admission
	/// came from its own request path). <paramref name="consumed"/> is the limb-tool
	/// family's observation that the run destroyed the item object; the eat reports
	/// false because its report is issued from inside the use call.
	/// </summary>
	public void SendItemActionOutcome(ulong itemInstanceId, float condition, bool consumed)
	{
		if (!_session.SessionActive || itemInstanceId == 0)
		{
			return;
		}

		var msg = new PlayerItemActionOutcomeMsg
		{
			ItemInstanceId = itemInstanceId,
			Condition = condition,
			Consumed = consumed,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleItemActionOutcome(_session.LocalSteamId, msg);
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.PlayerItemActionOutcome, msg);
	}

	/// <summary>
	/// Host only: the affected side ran a cross-player use — its outcome is committed
	/// onto the item's OWNER and published as the ordinary use result, so the owner's
	/// own item and every peer's clone learn it through the same path every other
	/// family uses. The refused cases and what the item became are
	/// <see cref="PlayerItemActionOutcomeService.HandleOutcome"/>'s, because the
	/// admitted use is what authorizes the write at all.
	/// </summary>
	public void HandleItemActionOutcome(ulong sender, PlayerItemActionOutcomeMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (_outcomes.HandleOutcome(sender, msg) is { } result)
		{
			PublishUse(result);
		}
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
			[.. msg.AppliedDose.Select(PlayerInteractionKernelCodec.FromLiquidStack)],
			msg.LimbIndex,
			[.. msg.DrinkDose.Select(PlayerInteractionKernelCodec.FromLiquidStack)],
			msg.TargetEatsTheItem,
			msg.TargetRunsLimbAction,
			out _,
			out var rejection))
		{
			_log.LogWarning("[ItemUse] kernel result rejected {User} -> {Target}: {Reason} ({Message}).",
				msg.UserSteamId, msg.TargetSteamId, rejection!.Reason, rejection.Message);
		}
	}
}
