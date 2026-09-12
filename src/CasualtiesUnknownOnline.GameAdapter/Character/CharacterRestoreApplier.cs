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
			CharacterComponentSync.Apply(body, health);
		}

		foreach (var limbData in data.Limbs)
		{
			if (limbData.Index < 0 || limbData.Index >= body.limbs.Length)
			{
				continue;
			}

			_mapper.Map(limbData, body.limbs[limbData.Index]);
			LimbComponentStateCodec.Apply(body.limbs[limbData.Index], limbData.Components);
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
		foreach (var itemData in data.Items)
		{
			if (itemData.SlotIndex < 0)
			{
				_wearables.RestoreWearable(itemData, body);
			}
			else
			{
				ItemStateCodec.RestoreItem(itemData, body);
			}
		}

		var handSlot = data.HandSlot - 1; // wire encoding: handSlot + 1
		if (handSlot >= 0 && handSlot < body.slots.Length)
		{
			body.handSlot = handSlot;
		}
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
