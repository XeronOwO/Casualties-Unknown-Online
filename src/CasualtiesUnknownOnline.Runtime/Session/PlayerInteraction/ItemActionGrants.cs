using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host's outstanding affected-side uses: one entry per (actor, item) the host
/// admitted a cross-player use for on the family whose effect runs on the affected
/// side's own client, naming the item's owner. It exists because the outcome
/// arrives from the AFFECTED side while the item belongs to somebody else, and the
/// item domain's own rule — a member may not report a change to another member's
/// carried item — must keep holding.
/// <para>
/// So the admission IS the grant: the host validated the pair and the item when it
/// admitted the use, and the one outcome report for that pair is the only write it
/// authorizes. An entry is consumed by the report it authorizes, so a repeat or a
/// late report never lands on an item whose use is already settled, and the whole
/// table dies with the session (a grant cannot outlive the admission it came from).
/// Bounded by construction: one entry per admitted use in flight, cleared on the
/// report that settles it.
/// </para>
/// </summary>
internal sealed class ItemActionGrants
{
	private readonly Dictionary<(ulong Actor, ulong ItemId), ulong> _grants = [];

	/// <summary>
	/// The host admitted a cross-player use of this item by this actor — remember who
	/// owns it. A pair that is already in flight keeps the FIRST admission: one
	/// admission authorizes one report, and overwriting it would let a second
	/// admission for the same pair extend the first one's life.
	/// </summary>
	internal void Grant(ulong actor, ulong itemId, ulong owner)
	{
		if (actor == 0 || itemId == 0 || owner == 0)
		{
			return;
		}

		var key = (actor, itemId);
		if (!_grants.ContainsKey(key))
		{
			_grants[key] = owner;
		}
	}

	/// <summary>
	/// Take the one grant this report settles. False means there is no admitted use
	/// for this pair: the report is refused rather than applied, because without the
	/// admission it would be one member writing another member's item.
	/// </summary>
	internal bool TryTake(ulong actor, ulong itemId, out ulong owner)
	{
		if (!_grants.TryGetValue((actor, itemId), out owner))
		{
			return false;
		}

		_grants.Remove((actor, itemId));
		return true;
	}

	/// <summary>Session ended: an admission belongs to the session that issued it.</summary>
	internal void Reset() => _grants.Clear();

	/// <summary>Diagnostics: how many admitted uses are in flight on this host.</summary>
	internal int Count => _grants.Count;
}
