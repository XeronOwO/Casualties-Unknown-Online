using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The carried-fact table per remote owner (SteamId → the latest character
/// snapshot) with the snapshot-divergence monitor: every carried move MUST
/// arrive as an event (use/slot/wear/pickup/drop); a snapshot that carries a
/// move the fact table never saw means the event chain missed it and the 1 Hz
/// snapshot covered it up — user rule, that is a logic gap and must be loud.
/// Events apply immediately (ApplyCarriedSync / ApplyCarriedInventory /
/// RemoveCarriedItem); snapshots apply after the divergence check (and the 1 Hz
/// snapshot replaces the table wholesale, which is why the event merges only
/// need to cover the window before it). The table is CUO's own sync fact —
/// "which items CUO tracks" is ours, "where the item is" is the game's.
/// Split out of CharacterDataSync when the 600-line gate demanded it.
/// </summary>
internal sealed class CloneFactTable(ILogger log)
{
	private readonly ILogger _log = log;

	/// <summary>SteamId → latest character snapshot: the remote clone's inventory rendering source.</summary>
	private readonly Dictionary<ulong, CharacterDataMsg> _cloneData = [];

	/// <summary>A clone's snapshot cache updated (SteamId) — the renderer re-renders that clone's carried items. Without this, the clone only rendered once at creation ("after the starting supplies, the peer never sees carried-item updates").</summary>
	public event Action<ulong>? CloneSnapshotUpdated;

	/// <summary>Read-only view for the clone renderer: latest snapshot per SteamId.</summary>
	internal IReadOnlyDictionary<ulong, CharacterDataMsg> CloneData => _cloneData;

	/// <summary>Session ended: the fact table is session-scoped — stale owners/items must never render into the next lobby's clones.</summary>
	internal void Clear() => _cloneData.Clear();

	/// <summary>
	/// A carried item's authoritative fact changed (host broadcast — a use
	/// flipped its state, a slot move re-homed it, a pickup brought it in):
	/// update the owner's fact-table entry by instance id and re-render the
	/// clone immediately — the 1 Hz snapshot stays as the fallback. An event
	/// the snapshot has not seen yet is skipped (its slot is unknown then —
	/// SlotKnown=false, or the whole item is coming on the next snapshot).
	/// SlotKnown=false keeps the fact table's existing slot: the event's -1 is
	/// "not in any slot or limb", never a real slot.
	/// </summary>
	internal void ApplyCarriedSync(ulong owner, CharacterItemMsg item, bool slotKnown)
	{
		if (!_cloneData.TryGetValue(owner, out var data))
		{
			_log.LogInformation("[CarriedSync] no snapshot for owner {Owner} yet — the 1 Hz snapshot will carry the change.", owner);
			return;
		}

		var idx = data.Items.FindIndex(i => i.InstanceId == item.InstanceId);
		if (idx < 0)
		{
			if (!slotKnown)
			{
				_log.LogInformation("[CarriedSync] {Type} (id {ItemId}) not in {Owner}'s snapshot and slot unknown — the 1 Hz snapshot will carry the change.", item.ItemId, item.InstanceId, owner);
				return;
			}

			// A freshly picked-up item — append it (the clone renders it in its
			// slot; a worn item's limb encoding matches the wear loop).
			data.Items.Add(item);
			_log.LogInformation("[CarriedSync] added {Type} (id {ItemId}) to {Owner}'s snapshot — re-rendering the clone.", item.ItemId, item.InstanceId, owner);
		}
		else
		{
			if (!slotKnown)
			{
				// A use whose slot could not be resolved (or a world entry) —
				// the item's place in the snapshot is unchanged.
				item.SlotIndex = data.Items[idx].SlotIndex;
			}

			data.Items[idx] = item;
			_log.LogInformation("[CarriedSync] applied {Type} (id {ItemId}) to {Owner}'s snapshot — re-rendering the clone.", item.ItemId, item.InstanceId, owner);
		}

		CloneSnapshotUpdated?.Invoke(owner);
	}

	/// <summary>
	/// The owner's starting supplies with self-assigned ids arrived (the guest
	/// reports its carried inventory once its generation finished): merge them
	/// into its fact table — the clone renders the supplies immediately and the
	/// snapshot divergence check sees the entries as already-known instead of a
	/// phantom pickup (the id binding happens between the guest's early id-less
	/// snapshots and its self-assignment; without the merge, every binding read
	/// as "a new carried item without an event"). The owner's 1 Hz snapshot
	/// replaces the table wholesale afterwards, so the merge only needs to cover
	/// the window before it.
	/// </summary>
	internal void ApplyCarriedInventory(ulong owner, IReadOnlyList<CharacterItemMsg> items)
	{
		if (_cloneData.TryGetValue(owner, out var data))
		{
			foreach (var item in items)
			{
				var idx = data.Items.FindIndex(i => i.InstanceId == item.InstanceId);
				if (idx >= 0)
				{
					data.Items[idx] = item; // a 1 Hz snapshot already carried it — the self-assigned id is the binding
				}
				else
				{
					data.Items.Add(item);
				}
			}
		}
		else
		{
			// No snapshot yet (the report rides ahead of the guest's first 1 Hz
			// snapshot) — seed the fact table so the merge above happens then.
			_cloneData[owner] = new CharacterDataMsg { OwnerSteamId = owner, Items = [.. items] };
		}

		_log.LogInformation("[CarriedSync] merged {Count} starting supplies into {Owner}'s snapshot.", items.Count, owner);
		CloneSnapshotUpdated?.Invoke(owner);
	}

	/// <summary>
	/// A carried item left an inventory into the world (the ItemDropped event —
	/// fired locally by the dropper and on every receiving side): remove it
	/// from every owner's fact table, top-level or nested in a container's
	/// contents. World-side drops (a container's spill) match nothing — harmless.
	/// </summary>
	internal void RemoveCarriedItem(ulong itemId)
	{
		foreach (var owner in _cloneData.Keys)
		{
			var data = _cloneData[owner];
			if (data.Items.RemoveAll(i => i.InstanceId == itemId) > 0)
			{
				_log.LogInformation("[CarriedSync] removed {ItemId} from {Owner}'s snapshot — re-rendering the clone.", itemId, owner);
				CloneSnapshotUpdated?.Invoke(owner);
				continue;
			}

			foreach (var entry in data.Items)
			{
				if (RemoveNested(entry, itemId))
				{
					_log.LogInformation("[CarriedSync] removed {ItemId} from {Owner}'s snapshot contents — re-rendering the clone.", itemId, owner);
					CloneSnapshotUpdated?.Invoke(owner);
					break;
				}
			}
		}
	}

	private static bool RemoveNested(CharacterItemMsg entry, ulong itemId)
	{
		foreach (var content in entry.Contents)
		{
			// The remove exits the loop immediately — no collection-modified
			// hazard from the foreach.
			if (content.InstanceId == itemId || RemoveNested(content, itemId))
			{
				entry.Contents.Remove(content);
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// An enemy bite arrived (the dedicated event — never the 1 Hz snapshot):
	/// update the victim's fact-table entry (the bitten limb + the body's
	/// venom/adrenaline/happiness, exact rebuild) and re-render its clone. A
	/// victim with no snapshot yet is skipped — the next snapshot carries it.
	/// </summary>
	internal void ApplyEnemyBite(EnemyBiteMsg msg)
	{
		if (!_cloneData.TryGetValue(msg.VictimSteamId, out var data))
		{
			_log.LogInformation("[EnemyBite] no snapshot for victim {Victim} yet — the 1 Hz snapshot will carry the bite.", msg.VictimSteamId);
			return;
		}

		EnemyTerminalStateApplier.ApplyBite(data, msg);
		_log.LogInformation("[EnemyBite] applied to {Victim}'s limb {Limb}.", msg.VictimSteamId, msg.Limb.Index);
		CloneSnapshotUpdated?.Invoke(msg.VictimSteamId);
	}

	/// <summary>
	/// A crystal lunge arrived (the dedicated event — never the 1 Hz snapshot):
	/// update the victim's fact-table entry (the hit limb + adrenaline/stamina,
	/// exact rebuild) and re-render its clone. A victim with no snapshot yet is
	/// skipped — the next snapshot carries it.
	/// </summary>
	internal void ApplyEnemyLunge(EnemyLungeMsg msg)
	{
		if (!_cloneData.TryGetValue(msg.VictimSteamId, out var data))
		{
			_log.LogInformation("[EnemyLunge] no snapshot for victim {Victim} yet — the 1 Hz snapshot will carry the lunge.", msg.VictimSteamId);
			return;
		}

		EnemyTerminalStateApplier.ApplyLunge(data, msg);
		_log.LogInformation("[EnemyLunge] applied to {Victim}'s limb {Limb}.", msg.VictimSteamId, msg.Limb.Index);
		CloneSnapshotUpdated?.Invoke(msg.VictimSteamId);
	}

	/// <summary>
	/// An enemy-proximity side effect arrived (the dedicated event — never the
	/// 1 Hz snapshot): update the victim's fact-table entry (the kind-specific
	/// body fields, exact rebuild) and re-render its clone. A victim with no
	/// snapshot yet is skipped — the next snapshot carries the effect.
	/// </summary>
	internal void ApplyEnemyEffect(EnemyEffectMsg msg)
	{
		if (!_cloneData.TryGetValue(msg.VictimSteamId, out var data))
		{
			_log.LogInformation("[EnemyEffect] no snapshot for victim {Victim} yet — the 1 Hz snapshot will carry the effect.", msg.VictimSteamId);
			return;
		}

		EnemyTerminalStateApplier.ApplyEffect(data, msg);
		_log.LogInformation("[EnemyEffect] applied {Kind} to {Victim}.", msg.Kind, msg.VictimSteamId);
		CloneSnapshotUpdated?.Invoke(msg.VictimSteamId);
	}

	/// <summary>
	/// A limb-latch event arrived (the dedicated event — never the 1 Hz
	/// snapshot): update the owner's fact-table limb + body entry (full
	/// terminal state, exact rebuild) and re-render its clone. An owner with
	/// no snapshot yet is skipped — the next snapshot carries it.
	/// </summary>
	internal void ApplyLimbStateEvent(ulong owner, LimbStateEventMsg msg)
	{
		if (!_cloneData.TryGetValue(owner, out var data))
		{
			_log.LogInformation("[LimbEvent] no snapshot for owner {Owner} yet — the 1 Hz snapshot will carry the change.", owner);
			return;
		}

		EnemyTerminalStateApplier.ApplyLimbState(data, msg);
		_log.LogInformation("[LimbEvent] applied {Limbs} limbs to {Owner}.", msg.Limbs.Count, owner);
		CloneSnapshotUpdated?.Invoke(owner);
	}

	/// <summary>Store one owner's snapshot and re-render its clone, after the
	/// divergence check — every carried move MUST arrive as an event (use/slot/
	/// pickup/wear/drop); a snapshot that carries a move the fact table never
	/// saw means the event chain missed it and the 1 Hz snapshot covered it up.
	/// User rule: an event that relies on the timed snapshot is a logic gap and
	/// must be loud. (A move whose event is still in flight trips the warning
	/// too — the slot diverged from the fact table — which is exactly the trace
	/// you want when hunting a missed event.)</summary>
	internal void ApplySnapshot(ulong owner, CharacterDataMsg data)
	{
		if (_cloneData.TryGetValue(owner, out var prev))
		{
			WarnOnDivergence(owner, prev, data);
		}

		_cloneData[owner] = data;
		CloneSnapshotUpdated?.Invoke(owner);
	}

	/// <summary>Instance-matched comparison between the fact table (event-driven)
	/// and the incoming snapshot — every carried fact that changes through
	/// operations must arrive as an event (use/slot/wear/pickup/drop); a
	/// snapshot carrying a change the fact table never saw means the event chain
	/// missed it and the 1 Hz snapshot covered it up. User rule: an event that
	/// relies on the timed snapshot is a logic gap and must be loud. Compared
	/// signals: new entries (a pickup without an event), vanished entries (a
	/// drop without an event), the slot (moves/wears) and the flashlight state
	/// (uses) — the carried facts that change exclusively through operations.
	/// Condition is NOT compared (decay moves it on its own, every snapshot
	/// would false-positive), and neither are ammo/charges (their drains ride
	/// operations that are local-compute by design — the snapshot is their
	/// legitimate channel). A change whose event is still in flight trips the
	/// warning too — exactly the trace you want when hunting a missed event.
	/// The arbitration's corrections do NOT cover this: they compare a REPORT
	/// against the authoritative record — a report that never arrives has
	/// nothing to correct, so the snapshot compare is the only observer.</summary>
	private void WarnOnDivergence(ulong owner, CharacterDataMsg prev, CharacterDataMsg next)
	{
		foreach (var item in next.Items)
		{
			if (item.InstanceId == 0)
			{
				continue; // unbound — nothing to compare against
			}

			var old = prev.Items.FirstOrDefault(i => i.InstanceId == item.InstanceId);
			if (old is null)
			{
				// The fact table never saw this carried item — it entered the
				// inventory without a pickup event (the arbitration's correction
				// only fires when the report arrives; a missing report has
				// nothing to correct, the snapshot carried it silently). The
				// starting supplies are exempt: their self-assigned ids merge
				// into the fact table the moment the guest's carried-inventory
				// report arrives (ApplyCarriedInventory) — a snapshot racing it
				// still reads as an id binding, which is a known, logged channel.
				_log.LogWarning("[CharSync] divergence for {Owner}'s {Type} (id {ItemId}): a new carried item the fact table never saw — a pickup without an event sync (the 1 Hz snapshot carried it).",
					owner, item.ItemId, item.InstanceId);
				continue;
			}

			if (old.SlotIndex != item.SlotIndex)
			{
				_log.LogWarning("[CharSync] divergence for {Owner}'s {Type} (id {ItemId}): slot {Old} → {New} — a carried move without an event sync (the 1 Hz snapshot carried it).",
					owner, item.ItemId, item.InstanceId, old.SlotIndex, item.SlotIndex);
			}

			if (UseState(old) != UseState(item))
			{
				_log.LogWarning("[CharSync] divergence for {Owner}'s {Type} (id {ItemId}): flashlight state {Old} → {New} — a use without an event sync (the 1 Hz snapshot carried it).",
					owner, item.ItemId, item.InstanceId, UseState(old), UseState(item));
			}
		}

		// A carried item the snapshot no longer has — it left the inventory
		// without a drop event (same correction-blind spot as the pickup).
		foreach (var old in prev.Items)
		{
			if (old.InstanceId == 0 || next.Items.Any(i => i.InstanceId == old.InstanceId))
			{
				continue;
			}

			_log.LogWarning("[CharSync] divergence for {Owner}'s {Type} (id {ItemId}): left the inventory without an event sync (the 1 Hz snapshot carried it).",
				owner, old.ItemId, old.InstanceId);
		}

		// Limb latches (broken/dismembered/dislocated) change exclusively
		// through the dedicated limb-state event — a snapshot carrying a latch
		// change the fact table never saw means the event chain missed it. Only
		// compared where BOTH snapshots have the limb (a snapshot seeded with
		// items only has no limbs yet — that is a first snapshot, not a
		// divergence), and never the continuous values (bleed/skin/muscle decay
		// on their own — they are the snapshot's legitimate channel, like item
		// condition).
		foreach (var limb in next.Limbs)
		{
			var old = prev.Limbs.FirstOrDefault(l => l.Index == limb.Index);
			if (old is null)
			{
				continue;
			}

			if (old.Broken != limb.Broken)
			{
				_log.LogWarning("[CharSync] divergence for {Owner}'s limb {Limb}: broken {Old} → {New} — a break/mend without an event sync (the 1 Hz snapshot carried it).",
					owner, limb.Index, old.Broken, limb.Broken);
			}

			if (old.Dismembered != limb.Dismembered)
			{
				_log.LogWarning("[CharSync] divergence for {Owner}'s limb {Limb}: dismembered {Old} → {New} — a dismember without an event sync (the 1 Hz snapshot carried it).",
					owner, limb.Index, old.Dismembered, limb.Dismembered);
			}

			if (old.Dislocated != limb.Dislocated)
			{
				_log.LogWarning("[CharSync] divergence for {Owner}'s limb {Limb}: dislocated {Old} → {New} — a dislocate/relocate without an event sync (the 1 Hz snapshot carried it).",
					owner, limb.Index, old.Dislocated, limb.Dislocated);
			}
		}
	}

	/// <summary>The CustomItemBehaviour.state value (flashlight modes — the
	/// carried state that changes exclusively through uses), or 0 when the item
	/// has no such component.</summary>
	private static int UseState(CharacterItemMsg item)
	{
		var behaviour = item.Components.FirstOrDefault(c => c.TypeName == nameof(CustomItemBehaviour));
		return behaviour?.Fields.FirstOrDefault(f => f.Name == "state")?.IntValue ?? 0;
	}
}
