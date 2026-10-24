using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One validated cross-player use, executed: the family for the gesture's ENTRY,
/// that family's branch, the commit the branch leaves and the authoritative result.
/// Split from <see cref="PlayerItemUseService"/> at the architecture line gate so
/// the session surface (requests, participant checks, the affected side's outcome
/// report) and the use itself read apart.
/// <para>
/// The entry is the one input a request can carry that the item's own data cannot:
/// a container the game marks both ways — a drinkable bag holding a health-usable
/// liquid — is the inventory use's drink AND the wound view's topical application,
/// and only the gesture says which was made (decision 246). The family comes from
/// <see cref="ItemUseFamilyClassifier"/>, asked ONCE; no branch below re-asks a
/// predicate to decide a different family.
/// </para>
/// <para>
/// The migrated families commit no target body state: the drain a topical or drink
/// gesture measured travels to the affected side, whose own client runs the
/// liquids' own <c>onHealthUse</c>/<c>onDrink</c>, and the eat and limb tool run
/// entirely there. The host's part of those is the admission the outcome report is
/// matched against plus the one result it publishes from that report.
/// </para>
/// </summary>
internal sealed class PlayerItemUseExecution(
	ISessionControl session,
	PlayerCharacterAccess characters,
	IItemControl items,
	PlayerInteractionResultAuthority resultAuthority,
	PlayerItemUseCommit commit,
	PlayerItemActionOutcomeService outcomes,
	ILimbUseSemantics limbUseSemantics,
	IConsumeSemantics consumeSemantics,
	IWearSemantics wearSemantics,
	ISolidFoodSemantics solidFoodSemantics,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly IItemControl _items = items;
	private readonly PlayerInteractionResultAuthority _resultAuthority = resultAuthority;
	private readonly PlayerItemUseCommit _commit = commit;
	private readonly PlayerItemActionOutcomeService _outcomes = outcomes;
	private readonly ILimbUseSemantics _limbUseSemantics = limbUseSemantics;
	private readonly IConsumeSemantics _consumeSemantics = consumeSemantics;
	private readonly IWearSemantics _wearSemantics = wearSemantics;
	private readonly ISolidFoodSemantics _solidFoodSemantics = solidFoodSemantics;
	private readonly ILogger _log = log;

	/// <summary>
	/// Shared cross-player use execution. The kernel authoritative item is
	/// consumed/updated on <paramref name="user"/>'s character data, the body
	/// effect is applied to <paramref name="target"/>'s character data, and the
	/// result event is published for both participants. The item lookup and
	/// replacement are recursive so container-nested remote items (trash bags,
	/// backpacks) work exactly like direct slots.
	/// </summary>
	internal bool TryExecute(
		ulong user,
		ulong target,
		ulong itemInstanceId,
		int limbIndex,
		float doseMl,
		ItemUseEntry entry,
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
			originalItem = CarriedItemUseTree.FindFirstUsable(userData.Items, entry, _limbUseSemantics, _consumeSemantics, _wearSemantics, _solidFoodSemantics);
			if (originalItem is null)
			{
				_log.LogWarning("[ItemUse] refused: {User} has no usable consumable to auto-select.", user);
				return false;
			}
		}

		// The ONE family verdict for this request: the entry (which gesture the player
		// made) x the item's own data. The two entries can put the same item in
		// different families — a drinkable bag holding a health-usable liquid is the
		// inventory use's drink and the wound view's topical application — and that is
		// the item being two actions, not a disagreement to arbitrate by order.
		var family = ItemUseFamilyClassifier.Classify(
			entry, originalItem.ItemId, originalItem.Liquids, _limbUseSemantics, _consumeSemantics, _wearSemantics, _solidFoodSemantics);

		// A condition-costing family (wear, eat, limb tool) spends the item's
		// condition, so an item with none left is not carried. The liquid families are
		// not gated here: their condition IS the fill level, so an empty container
		// carries no stacks to draw and the rules are false for it already.
		var carried = family != ItemUseFamily.None
			&& (originalItem.Condition > 0f || family is not (ItemUseFamily.Wear or ItemUseFamily.SolidFood or ItemUseFamily.LimbTool));
		if (!carried)
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

			_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) is empty or is not a family this {Entry} gesture carries.", originalItem.ItemId, originalItem.InstanceId, entry);
			return false;
		}

		// The TARGET's gate, asked AFTER the family is known because the rule is the family's: a body must be alive to
		// receive anything, and only the two families where the TARGET consumes the item (drink, eat) need it
		// conscious. Bandaging, splinting, injecting and applying a topical are exactly what an UNCONSCIOUS teammate
		// needs, so the old blanket "the target must be conscious" refused the case the medical gesture exists for
		// (the user's 2026-10-24 finding: the native gate it was copied from tests the LOCAL body, i.e. the actor).
		if (targetData.Health is not { } targetHealth || !targetHealth.Alive)
		{
			_log.LogInformation("[ItemUse] refused: {Target} is not alive and cannot receive an item.", target);
			return false;
		}

		if (ItemUseTargetPolicy.RequiresAConsciousTarget(family) && !targetHealth.Conscious)
		{
			_log.LogInformation("[ItemUse] refused: {Target} is unconscious and cannot {Family} an item.", target, family);
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
		// gesture asks and the classifier's wear arm asked (the same placement read,
		// asked once per site): a wearable can never be admitted and then fail to
		// resolve. The limb index comes back resolved because the character snapshot
		// encodes a worn item as -(index + 2); the game's occupancy check compares the
		// wear SLOT id, which is what makes two wearables in one slot collide even when
		// they name different limbs.
		if (family == ItemUseFamily.Wear)
		{
			if (!_wearSemantics.TryGetWearPlacement(originalItem.ItemId, out var wearLimbIndex, out var wearSlotId))
			{
				_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) resolved as a wearable but its placement could not be read.", originalItem.ItemId, originalItem.InstanceId);
				return false;
			}

			if (!RemoteWearApplication.TryCreateWornItem(_wearSemantics, wearLimbIndex, wearSlotId, newTargetData.Limbs, newTargetData.Items, originalItem, out wornItem))
			{
				_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) cannot be placed on {Target} — target limb missing/dismembered or wear slot already occupied.", originalItem.ItemId, originalItem.InstanceId, target);
				return false;
			}

			newTargetData.Items.Add(wornItem);
			destroyed = true; // the acting player's local item is removed; the wire carries WornItem for the target side
			_log.LogInformation("[ItemUse] {User} wears {ItemId} (id {InstanceId}) on {Target}; slot {Slot}.", user, originalItem.ItemId, originalItem.InstanceId, target, wornItem.SlotIndex);
		}
		else if (family == ItemUseFamily.Injection)
		{
			// The classifier asks this rule FIRST on both entries, before the drink
			// rule, because the items both rules admit are the liquid containers the
			// game can also draw from a limb (saline, ringersolution, a blood bag), and
			// for those the one-shot path has always refused by name and pointed at the
			// medical operation session. The limb rules ask their own flag
			// (usableOnLimb) while this chain asks `usable`, so the order — not the data
			// — is what keeps a dual-class container on the family the gesture was made
			// for. The refusal is what keeps an INJECTABLE container's own use action
			// away from the drink measurement. A usable container whose stacks carry no
			// injectable flag (a blood bag refilled with water or a health-usable
			// liquid) still reaches the drink family, but the measurement classifies the
			// DEFINITION before running it and refuses a non-drink action unrun
			// (`done/drink-measurement-destructive-use-action.md`), so this refusal is
			// not the only thing between a destructive action and the one-shot path.
			_log.LogWarning("[ItemUse] refused: injectable/IV medicine {ItemId} (id {InstanceId}) must use the medical operation session, not the one-shot request path.",
				originalItem.ItemId, originalItem.InstanceId);
			return false;
		}
		else if (family == ItemUseFamily.Topical)
		{
			// Only the LIMB entry reaches this arm: the inventory use's classifier never
			// asks the topical rule (decision 246). The decision and the plan live in
			// TopicalUseBranch: the request must name the limb the wound view carried
			// (that release IS the application's native call site) and must carry the
			// dose the operator's own item delegate measured. Nothing target-side is
			// computed or saved here.
			if (!TopicalUseBranch.TryPlan(limbIndex, target, originalItem, doseMl, newItem, _log, out appliedDose))
			{
				return false;
			}
		}
		else if (family == ItemUseFamily.Drink)
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
			// The classifier asks this family after the injection refusal on BOTH
			// entries, and on the limb entry after the topical rule; the operator's own
			// measurement asked the same classifier, so the dose and the dispatch name
			// one native call. Vanilla never needs the inner order (every topical
			// carrier is `usable=false`), but the container both entries admit — a
			// drinkable bag holding a health-usable liquid — must drink here exactly
			// when the wound view is not the entry.
			if (!LiquidDrainPlan.TryCreate(originalItem.Liquids, doseMl, out var drinkPlan))
			{
				_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) carried no dose to drink for {Target}.", originalItem.ItemId, originalItem.InstanceId, target);
				return false;
			}

			drinkDose = drinkPlan;
			CarriedItemUseTree.ApplyDrain(newItem, drinkPlan);
		}
		else if (family == ItemUseFamily.SolidFood)
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
		else if (family == ItemUseFamily.LimbTool)
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
			_outcomes.Admit(target, originalItem.InstanceId, user, limbIndex);
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
			_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) is not a family this {Entry} gesture carries.", originalItem.ItemId, originalItem.InstanceId, entry);
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

	/// <summary>
	/// Publish one committed use result through the kernel. The kernel is the single
	/// authority: the committed result event is broadcast through <c>KernelEnvelope</c>
	/// and the <see cref="PlayerInteractionKernelProjection"/> raises this same event
	/// on the host (BatchCommitted) and guests (BatchApplied); no legacy direct result
	/// wire remains.
	/// </summary>
	internal void PublishUse(PlayerItemUseResultMsg msg)
	{
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
			msg.BecameLimbComponent,
			out _,
			out var rejection))
		{
			_log.LogWarning("[ItemUse] kernel result rejected {User} -> {Target}: {Reason} ({Message}).",
				msg.UserSteamId, msg.TargetSteamId, rejection!.Reason, rejection.Message);
		}
	}
}
