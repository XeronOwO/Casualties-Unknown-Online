using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The WRITE half of a local character restore: what a snapshot is put onto once
/// <see cref="CharacterDataSync"/> has decided that a body must receive it.
///
/// Split out of <see cref="CharacterDataSync"/> — the coordinator owns WHEN a
/// restore runs (the two-frame wipe/items rhythm, the queue, the position gate,
/// the 1 Hz report) while the mapping of a snapshot onto a live body is its own
/// responsibility, and the coordinator had reached the architecture line gate.
/// The split is also what makes the phases readable as one contract: pass one
/// (<see cref="ApplyStatsAndWipe"/>) destroys the fresh world's state and writes
/// stats, pass two (<see cref="ApplyItems"/>) — a frame later, once the wipe's
/// end-of-frame destroy actually ran — puts the carried items back.
///
/// Every method here is deliberately dumb about the door it was called through:
/// the same code serves a host's own continue, a next-level respawn and a guest's
/// reconnect hand-over, so no role gets a different flavour of "my character came
/// back" (decision 170).
/// </summary>
internal sealed class CharacterRestoreApplier(
	IMapper mapper,
	WearableRestorer wearables,
	ICharacterNativeSystem nativeSystem,
	ILogger<CharacterRestoreApplier> log)
{
	private readonly IMapper _mapper = mapper;
	private readonly WearableRestorer _wearables = wearables;
	private readonly ICharacterNativeSystem _nativeSystem = nativeSystem;
	private readonly ILogger<CharacterRestoreApplier> _log = log;

	/// <summary>
	/// First pass: wipe the fresh-run default state and write the snapshot's
	/// stats. The wipe is what makes the second pass possible — the game's
	/// starting supplies (WorldGeneration.WorldPlacePlayer) and the random vitals
	/// (Body.Start) are already on the body when a restore runs, and restoring on
	/// top of them would duplicate the items and leave the random hunger/thirst
	/// in place.
	/// </summary>
	internal void ApplyStatsAndWipe(Body body, CharacterDataMsg data)
	{
		_log.LogInformation("Applying character restore ({Items} items).", data.Items.Count);

		// Destroy is end-of-frame; the items are re-added on the next frame
		// (CharacterDataSync's second pass), so the slots are actually empty when
		// PickUpItem runs — it silently refuses a non-empty slot
		// (Body.cs:1388) and the item would be stranded.
		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var holder = body.slots[slot].transform;
			for (var i = holder.childCount - 1; i >= 0; i--)
			{
				Object.Destroy(holder.GetChild(i).gameObject);
			}
		}

		ApplyStats(body, data, applyComponents: true);
	}

	/// <summary>
	/// The state half of a restore, split from the wipe because the host's simulated clone needs
	/// it without the item wipe and WITHOUT the component half. That body is the render clone: the
	/// limb components (<c>SplintLimb</c>/<c>TourniquetScript</c>/<c>ChilledLimb</c>) and the
	/// medication components run owner-local native behaviour, and their state is already carried
	/// by the mapped limb fields and the health block. Adding them enabled onto a render clone is
	/// exactly the per-frame exception storm <see cref="LimbComponentStateCodec"/> records for a
	/// display copy. A real restore still passes <c>applyComponents: true</c>.
	/// </summary>
	internal void ApplyStats(Body body, CharacterDataMsg data, bool applyComponents)
	{
		if (data.Skills is { } skills)
		{
			_mapper.Map(skills, body.skills);
			body.skills.UpdateExpBoundaries(); // min/max derive from STR/RES/INT (Skills.cs:61)
		}

		if (data.Health is { } health)
		{
			// Target-driven: only writable Body members that exist in the source
			// are touched — alive/conscious (derived properties, Body.cs:203/213)
			// are read-only and skipped automatically.
			_mapper.Map(health, body);
			if (applyComponents)
			{
				CharacterComponentSync.Apply(body, health);
			}
		}

		foreach (var limbData in data.Limbs)
		{
			if (limbData.Index < 0 || limbData.Index >= body.limbs.Length)
			{
				continue;
			}

			_mapper.Map(limbData, body.limbs[limbData.Index]);
			if (applyComponents)
			{
				LimbComponentStateCodec.ApplyAuthoritative(body.limbs[limbData.Index], limbData.Components);
			}
		}
	}

	/// <summary>
	/// Second pass: the carried items onto a body whose slots the previous frame's
	/// wipe emptied — the backpack items through the item codec, the worn ones onto
	/// their limb, and the hand slot last (a restored item in the hand must be the
	/// one the snapshot named).
	/// </summary>
	internal void ApplyItems(Body body, CharacterDataMsg data)
	{
		// The restore puts the snapshot's items back through the game's own slot path
		// (PickUpItem / Container.LoadItem), and those calls are item OPERATIONS: their
		// hooks report a pickup. A restore is not an operation — the host already holds
		// these facts — and batch 20260930-g's host log shows the consequence: the items
		// that reappeared were reported, and the kernel refused exactly them, "Conflict
		// (item … is already carried)". The refusal path that follows
		// (ItemApplication.OnItemRejected → RollbackPickup) pulls a restored item back OUT
		// of the body, so the report is at best noise and at worst a rollback of the state
		// just restored. It is NOT claimed to be that batch's only loss: the control
		// reconnect lost its dog food with no refusal at all, which is the flat-merge half
		// (TransferTableRestoreMerge).
		// The write half therefore runs under the same scope the item correction applies
		// already use (ItemApplication): RemoteApply keeps the item hooks from reporting
		// what a restore puts back (PickupSync, ContainerItemSync, ItemWorldSync, and the
		// drop-cancel / slot-rehome branches those coordinators own), so a restore states
		// nothing — it only reproduces state the peers already hold.
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			foreach (var itemData in data.Items)
			{
				if (itemData.SlotIndex >= 0)
				{
					ItemStateCodec.RestoreItem(itemData, body);
				}
				else if (itemData.SlotIndex <= -2)
				{
					_wearables.RestoreWearable(itemData, body);
				}
				else
				{
					// The archive states NO body slot for it: the cut landed while this member's body
					// carried the item and the report that states its slot had not arrived, so the
					// archive kept the kernel's placement and left the slot to the body it restores
					// onto (the format's unplaced row, -1). The body's own rules decide, exactly as
					// they do for a handed-over item — the archive never guesses a slot.
					PlaceUnplacedItem(itemData, body);
				}
			}

			var handSlot = data.HandSlot - 1; // wire encoding: handSlot + 1
			if (handSlot >= 0 && handSlot < body.slots.Length)
			{
				body.handSlot = handSlot;
			}
		}
	}

	/// <summary>
	/// One item the archive carried with no body slot (<see cref="CharacterItemMsg.SlotIndex"/> -1):
	/// materialize it and let this body's own slot rules place it. A body with no acceptable free slot
	/// is the one case that cannot land it; destroying the object ends the item on this client, so the
	/// refusal is NAMED here — the archive already named which item it was, and the kernel's row is
	/// the only carrier left.
	/// </summary>
	private void PlaceUnplacedItem(CharacterItemMsg itemData, Body body)
	{
		if (ItemStateCodec.MaterializeItem(itemData, body.transform.position) is not { } item)
		{
			_log.LogWarning(
				"Restore: unplaced {ItemId} (id {InstanceId}) could not be created on this client — it is not on the body.",
				itemData.ItemId, itemData.InstanceId);
			return;
		}

		if (LocalBodyPlacement.TryPlaceInSlot(body, itemData, item, _log, "CharacterRestore", out _))
		{
			return;
		}

		_log.LogWarning(
			"Restore: unplaced {ItemId} (id {InstanceId}) found no body slot this body accepts — its copy is destroyed; the cut carried no slot for it and this body refused every free one.",
			itemData.ItemId, itemData.InstanceId);
		Object.Destroy(item.gameObject);
	}

	/// <summary>
	/// The native character fields of a restored snapshot
	/// (<c>lastHappiness</c>, <c>caloriesConsumed</c>, <c>WoundView.cInfo</c>),
	/// written on the restore path's second pass — after the body exists, after the
	/// wipe and after the items, which is the same point the native load wrote them
	/// (<c>SaveSystem.cs:438-441</c>). The snapshot's own fields reach the adapter
	/// because a continue hands the archive's character back through the same local
	/// restore path a respawn and a reconnect hand-over use (decision 170).
	///
	/// Returns one entry per field, in the snapshot's order, saying what the live
	/// game did with it. The caller names every refusal — never a silent default
	/// (§6). A snapshot that carries no native fields applies nothing and the caller
	/// names the whole absence.
	/// </summary>
	internal IReadOnlyList<NativeFieldWrite> ApplyNativeFields(Body body, CharacterDataMsg data) =>
		data.NativeFields is { } fields
			? CharacterNativeFields.Apply(_nativeSystem, body, fields, data)
			: [];
}
