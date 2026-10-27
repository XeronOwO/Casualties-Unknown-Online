using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host's outstanding affected-side uses: one entry per (actor, item) the host
/// admitted a cross-player use for on the family whose effect runs on the affected
/// side's own client, naming the item's owner and — for the family that is about a
/// limb — the limb the host authorized. It exists because the outcome arrives from
/// the AFFECTED side while the item belongs to somebody else, and the item domain's
/// own rule — a member may not report a change to another member's carried item —
/// must keep holding.
/// <para>
/// So the admission IS the grant: the host validated the pair and the item when it
/// admitted the use, and the one outcome report for that pair is the only write it
/// authorizes. An entry is consumed by the report it authorizes, so a repeat or a
/// late report never lands on an item whose use is already settled, and the whole
/// table dies with the session (a grant cannot outlive the admission it came from).
/// Bounded by construction: one entry per admitted use in flight, cleared on the
/// report that settles it.
/// </para>
/// <para>
/// The LIMB rides the grant rather than the report because the host is where it came
/// from: it is the number the host put in the use result, the affected side resolves
/// exactly that number or refuses (<c>NativeLimbTarget.ResolveNamed</c>), and the
/// applied row the outcome commits is therefore on the limb the host already
/// authorized. Echoing it back would give one fact two carriers.
/// </para>
/// </summary>
internal sealed class ItemActionGrants
{
	/// <summary>
	/// Who owns the item, which limb the use was authorized on (-1 for a family with no limb), and the item's DEFINITION as it was when the host
	/// admitted the use. The definition rides the admission because the two reports are classified by it: a replacing action renames the row in one
	/// report, and the other report — the one carrying the pre-swap condition — must still be judged by what the item WAS, or it is read as an
	/// ordinary eat and zeroes the very condition that then destroys the owner's item (measured 2026-10-27, the peel).
	/// </summary>
	private readonly record struct Admission(ulong Owner, int LimbIndex, string DefinitionId);

	private readonly Dictionary<(ulong Actor, ulong ItemId), Admission> _grants = [];
	private readonly Dictionary<(ulong Actor, ulong ItemId), ulong> _followUps = [];
	private readonly Dictionary<(ulong Actor, ulong ItemId), string> _followUpDefinitions = [];

	/// <summary>
	/// The host admitted a cross-player use of this item by this actor — remember who
	/// owns it and, when the family names one, the limb. A pair that is already in
	/// flight keeps the FIRST admission: one admission authorizes one report, and
	/// overwriting it would let a second admission for the same pair extend the first
	/// one's life.
	/// </summary>
	internal void Grant(ulong actor, ulong itemId, ulong owner, string definitionId, int limbIndex = -1)
	{
		if (actor == 0 || itemId == 0 || owner == 0)
		{
			return;
		}

		var key = (actor, itemId);
		if (!_grants.ContainsKey(key))
		{
			_grants[key] = new Admission(owner, limbIndex, definitionId ?? string.Empty);
		}
	}

	/// <summary>
	/// Take the one grant this report settles. False means there is no admitted use
	/// for this pair: the report is refused rather than applied, because without the
	/// admission it would be one member writing another member's item.
	/// </summary>
	internal bool TryTake(ulong actor, ulong itemId, out ulong owner, out int limbIndex, out string definitionId)
	{
		if (!_grants.TryGetValue((actor, itemId), out var grant))
		{
			owner = 0;
			limbIndex = -1;
			definitionId = string.Empty;
			return false;
		}

		_grants.Remove((actor, itemId));

		// The ONE follow-up this admission allows: a use whose effect runs on the affected side and REPLACES the item reports the state in one
		// message and the replacement definition in another (the eating client only sees the object its delegate created after the fact). The
		// grant is consumed by the first, so the second would be refused — and was, which is why the owner's row kept its old definition and was
		// destroyed as an ordinary consumed item instead of becoming the replacement (2026-10-26). One admission therefore remembers itself for
		// exactly ONE follow-up from the same pair; a third report finds nothing, and the session boundary clears it like the grants.
		_followUps[(actor, itemId)] = grant.Owner;
		_followUpDefinitions[(actor, itemId)] = grant.DefinitionId;
		owner = grant.Owner;
		limbIndex = grant.LimbIndex;
		definitionId = grant.DefinitionId;
		return true;
	}

	/// <summary>
	/// The one follow-up an admission allows, taken at most once per (actor, item): the report that carries what the affected side's delegate
	/// REPLACED the item with. False for any other report, so a second write still needs its own admission. The definition comes back with it so the
	/// caller classifies the report by what the item WAS when it was admitted, never by what an earlier report renamed it to.
	/// </summary>
	internal bool TryTakeFollowUp(ulong actor, ulong itemId, out ulong owner, out string definitionId)
	{
		if (!_followUps.TryGetValue((actor, itemId), out owner))
		{
			owner = 0;
			definitionId = string.Empty;
			return false;
		}

		_followUps.Remove((actor, itemId));
		definitionId = _followUpDefinitions.TryGetValue((actor, itemId), out var known) ? known : string.Empty;
		_followUpDefinitions.Remove((actor, itemId));
		return true;
	}

	/// <summary>Session ended: an admission belongs to the session that issued it.</summary>
	internal void Reset()
	{
		_grants.Clear();
		_followUps.Clear();
		_followUpDefinitions.Clear();
	}

	/// <summary>Diagnostics: how many admitted uses are in flight on this host.</summary>
	internal int Count => _grants.Count;
}
