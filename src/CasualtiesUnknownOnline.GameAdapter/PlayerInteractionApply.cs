using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The local apply side of direct player interaction: cross-player inventory
/// transfer, carry/release and heals. The host sends one authoritative message
/// to each participant; this class mutates the LOCAL body (the only body this
/// process simulates) inside a RemoteApply scope and triggers the immediate
/// re-report so every peer's clone learns the real result in the same run.
/// It also owns the carry-follow transform update and the Online UI heal-item
/// projection.
/// </summary>
internal sealed class PlayerInteractionApply(GameAdapterDomains domains, IRemotePlacementResolver? placementResolver = null)
{
	private PlayerCarryStateMsg? _pendingCarryState;
	private readonly CarrySimulationTrace _carryTrace = new();
	private readonly PlayerInventoryPlacement _placement = new(domains);
	private readonly IRemotePlacementResolver? _placementResolver = placementResolver;

	public void OnPlayerInventoryTransfer(PlayerInventoryTransferMsg msg)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			domains.Log.LogWarning("[PlayerInteraction] transfer received but the local body is not ready — skipped (1 Hz snapshot will not apply this authoritative fact; a retry is required).");
			return;
		}

		var changed = false;
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (msg.Destination == PlayerInventoryDestination.Drop)
			{
				// The ladder's last rung: nothing on the body can take the item, so the host sent it here to be
				// dropped at THIS client's own position. It is not carried, so the inventory does not change and
				// no character report is sent; the placement reports the created world item itself.
				if (msg.ToSteamId == domains.Session.LocalSteamId && msg.Item is { } dropped)
				{
					_placement.DropCreatedItemAtLocalBody(body, dropped);
				}

				return;
			}

			if (msg.FromSteamId == domains.Session.LocalSteamId
				&& msg.ToSteamId == domains.Session.LocalSteamId
				&& msg.TargetParentItemId != 0
				&& msg.Item is { } sameOwnerItem)
			{
				// Same-owner container move: re-home the existing real item instead
				// of Destroy + RestoreContent. The destroy+rebuild path leaves the
				// old object alive until the end of the frame, so the immediate
				// re-report captures two children with the same instance id and the
				// container weight/display doubles for one frame.
				//
				// If the native container cannot accept the item, absolutely do NOT
				// destroy-and-rebuild. The old fallback removed the real source
				// first, then attempted a rebuild that could also be refused,
				// leaving the authoritative character/snapshot thinking the move
				// happened while the real body lost the item (the "drag out of a
				// trash bag makes the item vanish" family). Leave the original in
				// place so the next character report re-converges to the real body.
				if (_placement.TryMoveExistingItemToLocalContainer(body, sameOwnerItem, msg.TargetParentItemId))
				{
					changed = true;
				}
				else
				{
					domains.Log.LogWarning(
						"[PlayerInteraction] same-owner container move refused by the native container: {ItemId} (id {InstanceId}) stays in its current location; no destructive rebuild was performed.",
						sameOwnerItem.ItemId, sameOwnerItem.InstanceId);
				}
			}
			else
			{
				if (msg.FromSteamId == domains.Session.LocalSteamId)
				{
					_placement.RemoveCarriedItemFromLocalBody(body, msg.Item?.InstanceId ?? 0);
					changed = true;
				}

				if (msg.ToSteamId == domains.Session.LocalSteamId && msg.Item is { } item)
				{
					// The host recorded the destination this client ANSWERED before it wrote the row, so this
					// client may not substitute: if its live body can no longer take that exact place, it reports
					// a return comes home: any slot on this body is acceptable, and a body that accepts none must
					// not consume the item — it falls at this client's feet, the user's ruling being that the item
					// ends with the giver, never that a full body destroys it.
					if (msg.Returned && _placementResolver is not null && !_placementResolver.Resolve(item, item.SlotIndex, 0).Accepted)
					{
						domains.Log.LogWarning(
							"[PlayerInteraction] {ItemId} (id {InstanceId}) came back to a body with no room — dropping it at this client's feet.",
							item.ItemId, item.InstanceId);
						_placement.DropCreatedItemAtLocalBody(body, item);
						return;
					}

					// the failure and creates nothing — the host moves the row back to the giver.
					if (msg.ExactDestination && !CanTakeExactDestination(item, msg, out var exactRefusal))
					{
						domains.Log.LogWarning(
							"[PlayerInteraction] {ItemId} (id {InstanceId}) cannot take the answered destination: {Reason} — reporting the failure instead of substituting.",
							item.ItemId, item.InstanceId, exactRefusal);
						domains.PlayerInteraction.SendPlacementFailed(item.InstanceId, exactRefusal);
						return;
					}

					if (msg.TargetParentItemId != 0)
					{
						// One policy for both directions of a container destination: the container's own rules on this
						// client decide, and a refusal falls back to a slot here. A drop is NOT the answer for a
						// handed-over item — the host has already recorded its id as carried, so the spawn report a
						// drop needs is refused by the kernel and the object would be swept.
						_placement.AddCarriedItemToLocalContainer(body, item, msg.TargetParentItemId);
					}
					else
					{
						_placement.AddCarriedItemToLocalBody(body, item);
					}

					changed = true;
				}
			}
		}

		if (changed)
		{
			domains.CharacterDataSync.ReportInventoryChanged(body);
		}
	}

	/// <summary>
	/// Whether this client's live body can still take the exact destination it answered. It asks the same
	/// adapter port the answer came from — one resolution, so the answer and this check cannot drift — and a
	/// resolution that names a DIFFERENT slot is a refusal here: the host's row already states the answered place.
	/// </summary>
	private bool CanTakeExactDestination(CharacterItemMsg item, PlayerInventoryTransferMsg msg, out string reason)
	{
		reason = "";
		if (_placementResolver is null)
		{
			return true;
		}

		var answer = _placementResolver.Resolve(item, item.SlotIndex, msg.TargetParentItemId);
		var accepted = msg.TargetParentItemId != 0
			? answer.Accepted && answer.ContainerId == msg.TargetParentItemId
			: answer.Accepted && answer.Slot == item.SlotIndex;
		if (!accepted)
		{
			reason = string.IsNullOrWhiteSpace(answer.Reason)
				? "the answered destination is no longer available."
				: answer.Reason;
		}

		return accepted;
	}

	public void OnCarryStateChanged(PlayerCarryStateMsg msg)
	{
		_pendingCarryState = msg;
		var body = domains.Run.LocalBody; // Unity object — ==
		if (body == null)
		{
			return;
		}

		ApplyCarryStateToBody(body, msg);
		_pendingCarryState = null;
	}

	public void UpdateCarriedBody(Body? localBody)
	{
		if (localBody == null) // Unity object — ==
		{
			return;
		}

		if (_pendingCarryState is { } pending)
		{
			ApplyCarryStateToBody(localBody, pending);
			_pendingCarryState = null;
		}

		var driver = localBody.GetComponent<CarriedBodyDriver>();
		if (driver == null || driver.CarrierSteamId == 0)
		{
			return;
		}

		var mode = SimulationMode(localBody);
		// One trace tick per carried frame, whether or not a carrier anchor was
		// available: the frame count and the carry-follow count are only
		// comparable when the frames without an anchor are visible as such.
		_carryTrace.Tick(localBody, driver.CarrierSteamId, mode, domains.Log);

		// Use the remote carrier's RENDER clone as the anchor when it exists:
		// that clone is already smoothed by SessionStatePump, so the local rider
		// follows the same visual carrier position the player sees instead of
		// stepping to the raw 20 Hz buffer. The entity buffer is the fallback
		// before the clone exists (or if the carrier is not in the render table).
		if (domains.Renderer.TryGetRemoteBody(driver.CarrierSteamId, out var carrierBody)
			&& carrierBody != null) // Unity object — ==
		{
			CarriedBodyPlacement.ApplyLocalRiderPose(
				localBody,
				carrierBody.transform.position,
				carrierBody.isRight,
				carrierBody.crouching,
				carrierBody.rb.velocity,
				carrierBody.targetLookPos);
			TraceRider(localBody);
			return;
		}

		var carrier = domains.Entities.GetRemotePlayer(driver.CarrierSteamId);
		if (carrier is null)
		{
			return;
		}

		CarriedBodyPlacement.ApplyLocalRiderPose(
			localBody,
			new Vector3(carrier.Position.X, carrier.Position.Y, 0f),
			carrier.IsRight,
			carrier.Crouching,
			new Vector2(carrier.Velocity.X, carrier.Velocity.Y),
			new Vector2(carrier.LookPos.X, carrier.LookPos.Y));
		TraceRider(localBody);
	}

	/// <summary>
	/// The treatment the body patches will apply to this LOCAL body while it is
	/// carried: a conscious/alive rider keeps its own per-frame simulation, a dead
	/// or unconscious one keeps only the vitals half of it (its pose stays the
	/// pinned ragdoll the carry relation owns). The trace names it in the log.
	/// </summary>
	private static CarriedBodySimulation.Mode SimulationMode(Body body) =>
		CarriedBodySimulation.Treatment(isRemoteClone: false, isLocalCarriedBody: true, body.alive, body.conscious);

	/// <summary>One carry-follow write for the trace, then the immediate re-report.</summary>
	private void TraceRider(Body rider)
	{
		_carryTrace.CountCarryFollow();
		domains.Run.RefreshLocalBodyState();
	}

	public void OnPlayerHealReceived(PlayerHealResultMsg msg)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			domains.Log.LogWarning("[PlayerInteraction] heal result received but the local body is not ready — skipped.");
			return;
		}

		var changed = false;
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (msg.HealerSteamId == domains.Session.LocalSteamId)
			{
				var item = CarriedItemLocator.FindById(body, msg.ItemInstanceId);
				if (item == null) // Unity object — ==
				{
					domains.Log.LogWarning("[Heal] local healer item {ItemId} not found — consumed state skipped.", msg.ItemInstanceId);
				}
				else
				{
					if (msg.ItemDestroyed)
					{
						Object.Destroy(item.gameObject);
						domains.Log.LogInformation("[Heal] local item {ItemId} destroyed by cross-player heal.", msg.ItemInstanceId);
					}
					else
					{
						item.condition = msg.ItemConditionAfter;
						domains.Log.LogInformation("[Heal] local item {ItemId} condition set to {Condition:F2} by cross-player heal.", msg.ItemInstanceId, msg.ItemConditionAfter);
					}

					changed = true;
				}
			}

			if (msg.TargetSteamId == domains.Session.LocalSteamId && msg.Health is { } health)
			{
				domains.CharacterDataSync.ApplyHealState(body, health, msg.Limbs);
				domains.Log.LogInformation("[Heal] local body healed by {Healer} (limb {Limb}).", msg.HealerSteamId, msg.HealedLimbIndex);
				changed = true;
			}
		}

		if (changed)
		{
			domains.CharacterDataSync.ReportInventoryChanged(body);
		}
	}

	public void OnPlayerItemUseReceived(PlayerItemUseResultMsg msg)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			domains.Log.LogWarning("[PlayerInteraction] item-use result received but the local body is not ready — skipped.");
			return;
		}

		var changed = false;
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (msg.UserSteamId == domains.Session.LocalSteamId)
			{
				var item = CarriedItemLocator.FindById(body, msg.ItemInstanceId);
				if (item == null) // Unity object — ==
				{
					domains.Log.LogWarning("[ItemUse] local user item {ItemId} not found — consumed state skipped.", msg.ItemInstanceId);
				}
				else if (msg.ItemDestroyed)
				{
					if (msg.BecameLimbComponent)
					{
						// The destroy takes the owner's CARRIED object, not the item: the affected
						// side's own limb action turned it into the limb component, the commit
						// relocated its row onto that limb, and the instance id lives on. The same
						// marker the local apply seam attaches keeps the generic OnDestroy hook
						// from reporting this deferred destroy as the item's end — without it the
						// row that was just committed as Applied is overwritten with Terminal and
						// the limb keeps a component no row names (batch 20261018-u's row I1).
						item.gameObject.AddComponent<LimbAppliedItemOrigin>();
					}

					Object.Destroy(item.gameObject);
					domains.Log.LogInformation(
						msg.BecameLimbComponent
							? "[ItemUse] local item {ItemId} destroyed by cross-player use — the item did not end: it is installed on the target's limb and keeps its id."
							: "[ItemUse] local item {ItemId} destroyed by cross-player use.",
						msg.ItemInstanceId);
					changed = true;
				}
				else if (msg.ItemAfter is { } after)
				{
					item.condition = after.Condition;
					ItemStateCodec.RestoreLiquids(item, after.Liquids);
					ItemStateCodec.RestoreComponentStates(item, after.Components);
					domains.Log.LogInformation("[ItemUse] local item {ItemId} condition set to {Condition:F2} by cross-player use.", msg.ItemInstanceId, after.Condition);
					changed = true;
				}
			}

			if (msg.TargetSteamId == domains.Session.LocalSteamId)
			{
				if (msg.WornItem is { } worn)
				{
					domains.CharacterDataSync.RestoreWearable(worn, body);
					domains.Log.LogInformation("[ItemUse] local body wears {ItemId} from {User}.", worn.ItemId, msg.UserSteamId);
					changed = true;
				}
				else if (msg.Health is { } health)
				{
					domains.CharacterDataSync.ApplyHealState(body, health, msg.Limbs);
					domains.Log.LogInformation("[ItemUse] local body received a consumable from {User}.", msg.UserSteamId);
					changed = true;
				}
			}
		}

		// The migrated topical family is the one effect in this handler that is
		// EXECUTED rather than replayed: the host committed the drain and computed
		// no body state, and the game's own ApplyToLimb body runs here, on the body
		// it lands on. It therefore runs OUTSIDE the RemoteApply scope — that scope
		// is the "this is a replay of a peer's fact" marker, and SoundPlayPatch
		// refuses to relay any clip played inside it. A real effect's own clip must
		// be relayed exactly as it is when the patient applies the same liquid
		// locally, and nothing else in the handler keys off that scope. Part A's
		// injection chain keeps its own narrower scope, which is why a delegate clip
		// it plays (streptokinase) is still local to the patient.
		if (msg.TargetSteamId == domains.Session.LocalSteamId && msg.AppliedDose.Count > 0)
		{
			var handled = NativeTopicalApply.Apply(body, msg.ItemInstanceId, msg.LimbIndex, msg.AppliedDose, domains.Log);
			domains.Log.LogInformation(
				"[ItemUse] local body received {Count} topical stack(s) from {User} (limb {Limb}).",
				handled, msg.UserSteamId, msg.LimbIndex);
			// OR, not assignment, like the sibling halves above: the caller enforces
			// user != target today, so only one half can be for the local player, but
			// the report must not depend on that argument holding.
			changed |= handled > 0;
		}

		// The migrated drink family is executed for the same reason and under the
		// same scope rule as the topical half above: the host committed the drain
		// and computed no body state, and the liquids' own onDrink bodies run here,
		// on the body that drank. Outside the RemoteApply scope, so the clips those
		// delegates play (braingrow, antidepressants, antibiotics, antirad and
		// sleeping pills play "pills") are relayed to the peers exactly as they are
		// for a local drink.
		if (msg.TargetSteamId == domains.Session.LocalSteamId && msg.DrinkDose.Count > 0)
		{
			var drank = NativeDrinkApply.Apply(body, msg.DrinkDose, domains.Log);
			domains.Log.LogInformation(
				"[ItemUse] local body drank {Count} stack(s) from {User}.",
				drank, msg.UserSteamId);
			changed |= drank > 0;
		}

		// The solid-food family is EXECUTED here for the same reason and under the same
		// scope rule as the two families above, with one difference that makes it the
		// migration's end point: nothing was measured and nothing was committed, so this
		// call IS the whole effect. The game's own Body.UseItem runs the item's own use
		// action against THIS body and the standing object the host addressed — its own
		// local incarnation of the offered item — and the item's resulting state travels
		// back through the eater's report to the host, which hands it to the item's owner.
		if (msg.TargetSteamId == domains.Session.LocalSteamId && msg.TargetEatsTheItem)
		{
			changed |= NativeSolidFoodEat.Apply(body, msg.ItemInstanceId, domains);
		}

		// The limb tool is the eat's sibling and the last of the migration's five
		// families: nothing was measured and nothing was committed, so this call IS the
		// whole effect. The game's own useLimbAction runs against THIS body's limb (the
		// limb the operator picked, resolved against this body) and this client's own
		// object of the offered item, which is what makes the limb component, the timed
		// op and the item's condition cost the game's own code rather than a table's.
		if (msg.TargetSteamId == domains.Session.LocalSteamId && msg.TargetRunsLimbAction)
		{
			changed |= NativeLimbToolApply.Apply(body, msg.LimbIndex, msg.ItemInstanceId, domains);
		}

		if (changed)
		{
			domains.CharacterDataSync.ReportInventoryChanged(body);
		}
	}

	public bool HasLocalHealItem()
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return false;
		}

		// Recursive full-subtree scan: a heal item inside a carried container is
		// just as requestable as one in a direct slot, and the rest of this file
		// now resolves carried items recursively.
		foreach (var item in body.GetComponentsInChildren<Item>(true))
		{
			if (item == null || item.GetComponentInParent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				continue;
			}

			if (RemoteHealProfiles.IsHealItem(item.id))
			{
				return true;
			}
		}

		return false;
	}

	public IReadOnlyList<LocalHealItem> GetLocalHealItems()
	{
		var result = new List<LocalHealItem>();
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return result;
		}

		// Recursive full-subtree scan (same reason as HasLocalHealItem): the
		// selector must not miss heal items inside a carried container.
		foreach (var item in body.GetComponentsInChildren<Item>(true))
		{
			if (item == null || item.GetComponentInParent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				continue;
			}

			if (!RemoteHealProfiles.IsHealItem(item.id))
			{
				continue;
			}

			var id = item.GetComponent<ItemInstanceId>();
			if (id != null && id.Id != 0) // Unity object — ==
			{
				result.Add(new LocalHealItem(id.Id, item.id));
			}
		}

		return result;
	}

	private void ApplyCarryStateToBody(Body body, PlayerCarryStateMsg msg)
	{
		var driver = body.GetComponent<CarriedBodyDriver>();
		if (msg.CarriedSteamId == domains.Session.LocalSteamId)
		{
			if (driver == null) // Unity object — ==
			{
				driver = body.gameObject.AddComponent<CarriedBodyDriver>();
			}

			driver.CarrierSteamId = msg.CarrierSteamId;
			// The rider is NOT a render proxy any more: its own per-frame
			// simulation keeps running while the carry relation owns only the
			// transform, so nothing writes Body.standing here. The rigidbody
			// leaves the game's integrator before the first placement write, so
			// the body cannot be moved twice in the frame the relation starts.
			body.rb.simulated = false;
			CarrySimulationTrace.LogAttached(body, msg.CarrierSteamId, SimulationMode(body), domains.Log);
			return;
		}

		// A release for the relation this side was carrying (or a stale state
		// for a different carrier) clears the driver if this local body was the
		// carried half of that same relation.
		if (driver != null && driver.CarrierSteamId == msg.CarrierSteamId && msg.CarriedSteamId == 0)
		{
			var carrier = domains.Entities.GetRemotePlayer(msg.CarrierSteamId);
			var releasePosition = carrier is not null
				? new Vector3(carrier.Position.X, carrier.Position.Y, 0f)
				: (Vector3?)null;
			if (releasePosition is { } pos)
			{
				body.transform.position = pos;
			}

			// Empty the carrier id BEFORE the deferred Destroy: Unity keeps the
			// component until the end of the frame, and the render-proxy patches
			// decide by active-carrier state. Without this, one more proxy frame
			// can run after RestoreLocalBody and re-freeze the body/limbs.
			driver.CarrierSteamId = 0;
			CarriedBodyPlacement.RestoreLocalBody(body);
			Object.Destroy(driver);
			CarrySimulationTrace.LogReleased(body, msg.CarrierSteamId, domains.Log);
		}
	}

}
