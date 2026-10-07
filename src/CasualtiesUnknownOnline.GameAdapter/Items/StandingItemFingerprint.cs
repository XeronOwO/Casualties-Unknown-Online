using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// One owner's carried item tree as the standing materializer's last reconcile saw it, by REFERENCE: the
/// list instance, its element instances and their contents, recursively. The fact table rewrites what it
/// changes — a snapshot replaces the whole message (and its list), a carried fact or a nested move replaces
/// an element, a drop removes one — so a tree whose references all still match has not changed and the
/// objects built from it are already aligned.
///
/// <para>
/// WHY IT EXISTS. The carried-fact edge is not item-only: it also fires for an enemy bite/lunge/effect, a
/// medical state and a limb event, because the clone renderer re-renders on those too. A fire that left
/// every list and entry instance in place is answered by one walk of this tree instead of a plan rebuild
/// and a reflection-backed digest of every row — while a tree that DID change still gets the full realign,
/// which is what keeps a local copy's own decay corrected against the data (§6.8). The comparison is exact
/// (instance, count and order, recursively), and it errs towards reconciling: a tree the fact table
/// rebuilt with equal contents still fails it, which costs one reconcile and never a stale object.
/// </para>
///
/// <para>
/// WHERE IT CAN BE FOOLED, AND WHY THAT IS STILL SAFE. An in-place FIELD write to an element the fact table
/// already stores keeps every reference in place, and two host-side writers do exactly that:
/// <c>ItemArbitration.AdoptEvidence</c> rewrites the condition/liquids/components of a transfer-table entry
/// (registered un-copied, and that same instance is what <c>CarriedInventoryHandler</c> hands the clone
/// table when it fires the carried inventory), and <c>TransferTableRestoreMerge.TakeState</c> writes the
/// authoritative state onto the nodes of a stored snapshot (which <c>CharacterDataHandler</c> saves and then
/// fires, so the clone table stored that instance). Neither makes this an unbounded gate, because the
/// owner's own 1 Hz character report is deserialized FRESH: <c>ApplySnapshot</c> replaces the whole message
/// and its list every second, the comparison fails on the list instance alone, and the full digest reconcile
/// runs — a missed in-place write is therefore corrected within one interval at worst. That is the safety
/// net a later change must not remove: if the snapshot ever stops being a fresh instance (a reused message,
/// a content-diff before storing, a cached snapshot), the delay becomes a gate that can miss a change for
/// good. Its truth table, the blind spot included, is pinned by <c>StandingItemFingerprintTests</c>.
/// </para>
/// </summary>
internal sealed class StandingItemFingerprint
{
	private object? _list;
	private CharacterItemMsg[] _items = [];
	private StandingItemFingerprint[] _contents = [];

	/// <summary>Record a tree: the list, its elements and their contents, recursively.</summary>
	internal static StandingItemFingerprint Of(List<CharacterItemMsg> items)
	{
		var fingerprint = new StandingItemFingerprint
		{
			_list = items,
			_items = [.. items],
			_contents = new StandingItemFingerprint[items.Count],
		};

		for (var i = 0; i < items.Count; i++)
		{
			fingerprint._contents[i] = Of(items[i].Contents);
		}

		return fingerprint;
	}

	/// <summary>Whether the tree is the SAME INSTANCES, in the same order, all the way down.</summary>
	internal bool Matches(List<CharacterItemMsg> items)
	{
		if (!ReferenceEquals(_list, items) || _items.Length != items.Count)
		{
			return false;
		}

		for (var i = 0; i < _items.Length; i++)
		{
			if (!ReferenceEquals(_items[i], items[i]) || !_contents[i].Matches(items[i].Contents))
			{
				return false;
			}
		}

		return true;
	}
}
