using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host's outstanding solid-food eats: one entry per (eater, item) the host
/// admitted a cross-player eat for, naming the item's owner. It exists because
/// the eat's outcome arrives from the EATER while the item belongs to somebody
/// else, and the item domain's own rule — a member may not report a change to
/// another member's carried item — must keep holding.
/// <para>
/// So the admission IS the grant: the host validated the pair and the item when
/// it admitted the use, and the one outcome report for that pair is the only
/// write it authorizes. An entry is consumed by the report it authorizes, so a
/// repeat or a late report never lands on an item whose eat is already settled,
/// and the whole table dies with the session (a grant cannot outlive the
/// admission it came from). Bounded by construction: one entry per admitted eat
/// in flight, cleared on the report that settles it.
/// </para>
/// </summary>
internal sealed class SolidFoodEatGrants
{
	private readonly Dictionary<(ulong Eater, ulong ItemId), ulong> _grants = [];

	/// <summary>The host admitted a cross-player eat of this item by this eater — remember who owns it.</summary>
	internal void Grant(ulong eater, ulong itemId, ulong owner)
	{
		if (eater == 0 || itemId == 0 || owner == 0)
		{
			return;
		}

		_grants[(eater, itemId)] = owner;
	}

	/// <summary>
	/// Take the one grant this report settles. False means there is no admitted
	/// eat for this pair: the report is refused rather than applied, because
	/// without the admission it would be one member writing another member's
	/// item.
	/// </summary>
	internal bool TryTake(ulong eater, ulong itemId, out ulong owner)
	{
		if (!_grants.TryGetValue((eater, itemId), out owner))
		{
			return false;
		}

		_grants.Remove((eater, itemId));
		return true;
	}

	/// <summary>Session ended: an admission belongs to the session that issued it.</summary>
	internal void Reset() => _grants.Clear();

	/// <summary>Diagnostics: how many eats are in flight on this host.</summary>
	internal int Count => _grants.Count;
}
