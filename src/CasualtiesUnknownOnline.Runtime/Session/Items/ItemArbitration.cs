using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Host-side item ownership arbitration: the transfer table (which items each
/// guest currently owns — entries taken from the world table at pickup, so the
/// host always has the authoritative state of what a guest carries,
/// independent of the character-data snapshot the guest reports) plus the
/// evidence checks of every arbitrated action (pickup/drop/use/slot) and the
/// guest-side correction entry. Accept-with-correction: the action is never
/// blocked, only its evidence is compared — divergence syncs (correction
/// packet, one-shot ItemDestroy for claimed-but-unknown contents), never
/// rejects. Split out of ItemService when the 600-line gate demanded it — its
/// state (the transfer table) belongs here, ItemService forwards.
/// </summary>
public sealed class ItemArbitration(ISessionControl session, PacketSender sender, ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger _log = log;

	/// <summary>
	/// The transfer table (host only): which items each guest currently owns.
	/// Every arbitrated action moves/updates entries here — the entry is TAKEN
	/// from the world table at pickup (the host's own data, never the picker's
	/// claim). Used for action evidence checks, corrections and the reconnect
	/// restore merge. Session-scoped and layer-spanning: a guest's carried
	/// items survive layer changes and disconnects (reconnect recovery).
	/// </summary>
	private readonly Dictionary<ulong, Dictionary<ulong, WorldItem>> _transferred = [];

	/// <summary>Guest side: the host's authoritative item state arrived (our action-report evidence diverged) — the adapter applies it.</summary>
	public event Action<CharacterItemMsg>? ItemCorrectionReceived;

	/// <summary>
	/// Host only: whether the item is currently recorded as owned by the guest
	/// (the transfer table). The duplicate-pickup-report guard: a pickup report
	/// for an item the sender ALREADY owns is a retransmission (the transfer
	/// took the item out of the world table), never a rejection — the same
	/// idempotency family as the spawn/drop duplicate guards.
	/// </summary>
	public bool IsTransferredToGuest(ulong guest, ulong itemId) =>
		_transferred.TryGetValue(guest, out var owned) && owned.ContainsKey(itemId);

	// ===== Action entry points (ItemService forwards the wire reports here) =====

	/// <summary>
	/// Host only: a pickup was accepted — the entry (taken from the world table
	/// by the caller) becomes the picker's owned item. The picker's evidence is
	/// checked against it first; divergence syncs, never blocks. Returns the
	/// authoritative carried item (the transfer-table entry, slot adopted from
	/// the evidence) — the caller broadcasts it as the carried-fact event.
	/// </summary>
	public CharacterItemMsg CheckAndTransferToGuest(ulong guest, ulong itemId, WorldItem entry, CharacterItemMsg? evidence)
	{
		ApplyVerdict(guest, itemId, entry.Item, CheckEvidence(itemId, entry.Item, evidence));
		// The evidence carries the slot the picker's item landed in — adopted
		// (a carried item's slot is its owner's local fact, never corrected);
		// the reconnect restore needs a real slot or the item would not restore.
		// -1 is the only meaningless slot (not in any slot or limb); the limb
		// wear encodings (≤ -2) are real slots, adopted like the backpack ones.
		if (evidence is { SlotIndex: not -1 })
		{
			entry.Item.SlotIndex = evidence.SlotIndex;
		}

		if (!_transferred.TryGetValue(guest, out var owned))
		{
			_transferred[guest] = owned = [];
		}

		owned[itemId] = entry;
		return entry.Item;
	}

	/// <summary>
	/// Host only: a drop was reported — the item leaves the transfer table back
	/// into the world. The full drop item IS the evidence (the materialization
	/// payload), checked against the entry BEFORE it leaves.
	/// </summary>
	public void CheckAndUnloadFromGuest(ulong guest, ulong itemId, CharacterItemMsg evidence)
	{
		if (_transferred.TryGetValue(guest, out var owned) && owned.TryGetValue(itemId, out var transferred))
		{
			ApplyVerdict(guest, itemId, transferred.Item, CheckEvidence(itemId, transferred.Item, evidence));
			owned.Remove(itemId);
		}
	}

	/// <summary>
	/// Host only: an item was used — the guest is the fact source for its own
	/// body (the host's record is one action behind), so the used item's state
	/// is adopted UNCONDITIONALLY. A use changes the item's state by definition
	/// (a flashlight mode ++, a bite of food), so comparing the evidence against
	/// the host's one-action-behind record would correct every use back — the
	/// evidence can never match, the correction bounces the guest's action.
	/// Pickup/drop evidence still goes through CheckEvidence (a pickup is not a
	/// state change — a correction there converges the picker onto the host's
	/// record); a use is exactly the opposite. Returns the adopted authoritative
	/// item (null when untracked — no entry to arbitrate against).
	/// </summary>
	public CharacterItemMsg? CheckUseEvidence(ulong guest, ulong itemId, CharacterItemMsg evidence)
	{
		if (!_transferred.TryGetValue(guest, out var owned) || !owned.TryGetValue(itemId, out var entry))
		{
			// Not tracked: no entry to arbitrate against — the item predates
			// the transfer table (e.g. the reconnect restore has not merged
			// yet). Log and leave; the character-data snapshot path still
			// carries the state.
			_log.LogWarning("Item use {ItemId} from {Guest} — no transfer-table entry, not arbitrated.", itemId, guest);
			return null;
		}

		entry.Item.Condition = evidence.Condition;
		entry.Item.Favourited = evidence.Favourited;
		entry.Item.Liquids = evidence.Liquids;
		entry.Item.Components = evidence.Components;

		_log.LogInformation("Item {ItemId} used by {Guest}.", itemId, guest);
		return entry.Item;
	}

	/// <summary>
	/// Host only: an item moved slots — the guest's own slot layout is its
	/// local fact, recorded, never corrected (the slot rides in the
	/// authoritative item for the reconnect merge). Returns the updated
	/// authoritative item (null when untracked).
	/// </summary>
	public CharacterItemMsg? RecordSlot(ulong guest, ulong itemId, int slotIndex)
	{
		if (_transferred.TryGetValue(guest, out var owned) && owned.TryGetValue(itemId, out var entry))
		{
			entry.Item.SlotIndex = slotIndex;
			_log.LogInformation("Item {ItemId} moved to slot {Slot} by {Guest}.", itemId, slotIndex, guest);
			return entry.Item;
		}

		_log.LogWarning("Item slot {ItemId} from {Guest} — no transfer-table entry, not tracked.", itemId, guest);
		return null;
	}

	/// <summary>Guest side: the host's authoritative item state arrived — surface it for the adapter to apply.</summary>
	public void FireCorrectionReceived(CharacterItemMsg item)
	{
		_log.LogInformation("Item correction received: {ItemId} (Instance {InstanceId}).", item.ItemId, item.InstanceId);
		ItemCorrectionReceived?.Invoke(item);
	}

	/// <summary>
	/// True when the id appears inside any world-table entry's contents
	/// (recursive) — the item travels INSIDE a container entry, not as an
	/// independent world item. A pickup report of that id is then not "unknown":
	/// the container's own transfer carries the item, and refusing it would
	/// yank the content out of the picker's bag (a bag with contents picked up
	/// came back empty — the host's refusal rolled each content back into the
	/// world).
	/// </summary>
	public bool IsContainedInEntry(ulong itemId, IReadOnlyDictionary<ulong, WorldItem> worldItems)
	{
		foreach (var entry in worldItems.Values)
		{
			if (ContentsContain(entry.Item, itemId))
			{
				return true;
			}
		}

		return false;
	}

	private static bool ContentsContain(CharacterItemMsg item, ulong itemId) =>
		item.Contents.Any(c => c.InstanceId == itemId || ContentsContain(c, itemId));

	/// <summary>
	/// Host only: a guest's carried inventory with self-assigned ids arrived
	/// (its local generation finished — the starting supplies and worn items).
	/// Registered in the transfer table so the guest's use/slot reports
	/// arbitrate normally — the authoritative record the accept-with-correction
	/// path checks against (the guest's own report was the fact source before
	/// this, through the no-entry fallback). Idempotent: a re-report overwrites.
	/// </summary>
	public void RegisterCarried(ulong guest, IReadOnlyList<CharacterItemMsg> items)
	{
		if (!_transferred.TryGetValue(guest, out var owned))
		{
			_transferred[guest] = owned = [];
		}

		var registered = 0;
		foreach (var item in items)
		{
			if (item.InstanceId == 0)
			{
				continue; // unbound — nothing to register
			}

			owned[item.InstanceId] = new WorldItem(item.InstanceId, item, default, default, 0, 0f, false);
			registered++;
		}

		_log.LogInformation("Registered {Registered}/{Count} carried items of {Guest} in the transfer table.", registered, items.Count, guest);
	}

	// ===== Host-only surface =====

	public void SendCorrection(ulong targetSteamId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.ItemCorrection, new ItemCorrectionMsg { Item = item });
	}

	public IReadOnlyList<WorldItem> GetTransferredItems(ulong steamId)
		=> _transferred.TryGetValue(steamId, out var owned) ? [.. owned.Values] : [];

	// ===== Evidence check (host only) =====

	/// <summary>
	/// Compare the action-report evidence against the authoritative entry —
	/// PURE (no state, no sends, no logging): input message + entry, output the
	/// decision (<see cref="EvidenceVerdict"/>). The side effects the verdict
	/// demands run in <see cref="ApplyVerdict"/>. Accept-with-correction: the
	/// action is never blocked, only its evidence is checked. Only what the
	/// digest CLAIMS is compared: an empty nested Contents level means "no
	/// claim" (the digest shape stops at ids), never "empty contents" — that is
	/// what keeps a one-level digest from reading as missing against a
	/// two-level authoritative entry.
	/// </summary>
	internal static EvidenceVerdict CheckEvidence(ulong itemId, CharacterItemMsg authoritative, CharacterItemMsg? evidence)
	{
		if (evidence == null)
		{
			return EvidenceVerdict.Match;
		}

		// The authoritative entry's primary key must always be the table key —
		// the correction's recipient locates its instance BY it (entries are
		// captured id-less; the key is only known at arbitration time).
		authoritative.InstanceId = itemId;

		var matched = TopLevelMatches(evidence, authoritative);
		var missing = !ContentsMatch(evidence, authoritative, out var extra);
		return EvidenceVerdict.From(matched && !missing, extra);
	}

	/// <summary>
	/// Execute a verdict's side effects: the guest claims content ids the host
	/// does not have → each is destroyed with a one-shot ItemDestroy (never
	/// corrected back — they are not ours); top-level state or missing contents
	/// → the whole authoritative entry is sent as one correction (the guest's
	/// apply materializes missing contents and fixes state).
	/// </summary>
	private void ApplyVerdict(ulong guest, ulong itemId, CharacterItemMsg authoritative, EvidenceVerdict verdict)
	{
		foreach (var id in verdict.ExtraContentIds)
		{
			_sender.Send(guest, NetMsg.ItemDestroy, new ItemDestroyMsg { ItemId = id });
		}

		if (verdict.ExtraContentIds.Count > 0)
		{
			_log.LogWarning("Item {ItemId} evidence of {Guest} claims unknown contents [{Extra}] — destroying.", itemId, guest, string.Join(", ", verdict.ExtraContentIds));
		}

		if (verdict.NeedsCorrection)
		{
			SendCorrection(guest, authoritative);
			_log.LogInformation("Item {ItemId} evidence of {Guest} diverged — correction sent.", itemId, guest);
		}
	}

	/// <summary>Top-level state: condition (tolerance), favourited, liquid stacks and [Saveable] component states. Contents are compared separately (id sets).</summary>
	private static bool TopLevelMatches(CharacterItemMsg evidence, CharacterItemMsg authoritative)
		=> Math.Abs(evidence.Condition - authoritative.Condition) < 0.01f
			&& evidence.Favourited == authoritative.Favourited
			&& LiquidsMatch(evidence.Liquids, authoritative.Liquids)
			&& ComponentsMatch(evidence.Components, authoritative.Components);

	private static bool LiquidsMatch(List<LiquidStackMsg> evidence, List<LiquidStackMsg> authoritative)
	{
		if (evidence.Count != authoritative.Count)
		{
			return false;
		}

		foreach (var e in evidence)
		{
			var a = authoritative.FirstOrDefault(l => l.LiquidId == e.LiquidId);
			if (a == null || Math.Abs(a.Amount - e.Amount) >= 0.01f)
			{
				return false;
			}
		}

		return true;
	}

	private static bool ComponentsMatch(List<ComponentStateMsg> evidence, List<ComponentStateMsg> authoritative)
	{
		if (evidence.Count != authoritative.Count)
		{
			return false;
		}

		foreach (var e in evidence)
		{
			var a = authoritative.FirstOrDefault(c => c.TypeName == e.TypeName);
			if (a == null || e.Fields.Count != a.Fields.Count)
			{
				return false;
			}

			foreach (var ef in e.Fields)
			{
				var af = a.Fields.FirstOrDefault(f => f.Name == ef.Name);
				if (af == null || !FieldEquals(ef, af))
				{
					return false;
				}
			}
		}

		return true;
	}

	private static bool FieldEquals(ComponentFieldMsg e, ComponentFieldMsg a) => e.Kind switch
	{
		1 => Math.Abs(e.FloatValue - a.FloatValue) < 0.01f,
		2 => e.IntValue == a.IntValue,
		3 => e.BoolValue == a.BoolValue,
		4 => e.StringValue == a.StringValue,
		5 => e.StringList.SequenceEqual(a.StringList),
		_ => false,
	};

	/// <summary>
	/// Compare the evidence's contents against the authoritative entry's.
	/// Returns false when the authority has content ids the evidence does not
	/// claim (missing — the caller corrects); ids the evidence claims that the
	/// authority lacks are collected into extra (the caller destroys them).
	/// Nested levels are compared only where the evidence makes a claim.
	/// </summary>
	private static bool ContentsMatch(CharacterItemMsg evidence, CharacterItemMsg authoritative, out List<ulong> extra)
	{
		extra = [];
		var claimed = evidence.Contents.Where(c => c.InstanceId != 0).Select(c => c.InstanceId).ToHashSet();
		if (authoritative.Contents.Any(c => c.InstanceId != 0 && !claimed.Contains(c.InstanceId)))
		{
			return false;
		}

		foreach (var e in evidence.Contents)
		{
			if (e.InstanceId == 0)
			{
				continue;
			}

			var a = authoritative.Contents.FirstOrDefault(c => c.InstanceId == e.InstanceId);
			if (a == null)
			{
				extra.Add(e.InstanceId);
				continue;
			}

			if (e.Contents.Count == 0)
			{
				continue; // no nested claim — the digest stops at ids
			}

			if (!ContentsMatch(e, a, out var innerExtra))
			{
				return false;
			}

			extra.AddRange(innerExtra);
		}

		return true;
	}
}
