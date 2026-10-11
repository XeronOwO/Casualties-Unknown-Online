using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The local player's OWN favourite marks: the items somebody else owns that this
/// player starred. The mark is a local, personal act (user ruling 2026-10-11) —
/// it is never applied to the owner's item, never sent as an action on it, and
/// the owner's own favourite state is never what this player's view shows.
/// <para>
/// The ledger holds no game types and no scene: it is the decision half, so the
/// rules below are covered without a running game. What it does NOT own is
/// persistence — the marks ride the marker's own character record
/// (<see cref="CharacterDataMsg.FavouriteMarks"/>), which the world archive
/// stores per world, so <see cref="Seed"/> replaces the whole set whenever this
/// player's character comes back to them.
/// </para>
/// <para>
/// One live set per process, like the release window: a local client can only be
/// one player in one world.
/// </para>
/// </summary>
internal sealed class LocalFavouriteMarks
{
	/// <summary>The marks, keyed by the marked item's authoritative instance id (ids embed their creator, so an id names one item per session).</summary>
	private readonly Dictionary<ulong, CharacterFavouriteMarkMsg> _marks = [];

	/// <summary>How many marks are held — the capture logs it, and it is what a test asserts.</summary>
	internal int Count => _marks.Count;

	/// <summary>
	/// The view's one question: does this live item wear the local player's mark?
	/// The mark is on the ITEM, so the answer follows the item: the recorded owner is
	/// context and is refreshed whenever the item is seen with a new one (an item handed
	/// from one player to another is still the item this player starred, and it has to
	/// still be marked when it finally reaches this player — <see cref="TrySpend"/>).
	/// What DID change is the definition: a mark whose id now names an item of another
	/// definition is DROPPED, so a key the world has moved on from cannot light up on
	/// whatever inherited its id.
	/// <para>
	/// <paramref name="note"/> carries the one thing the caller cannot see for itself:
	/// with a TRUE answer it is the hand-off this call followed (the item moved to
	/// another owner), and with a false one it is the stale mark this call dropped.
	/// One channel, because both are the same event — the world moved on from what the
	/// mark recorded — and the caller reports the one it got.
	/// </para>
	/// </summary>
	internal bool IsMarked(ulong itemInstanceId, ulong ownerSteamId, string itemId, out string? note)
	{
		note = null;
		if (itemInstanceId == 0 || !_marks.TryGetValue(itemInstanceId, out var mark))
		{
			return false;
		}

		if (mark.ItemId != itemId)
		{
			_marks.Remove(itemInstanceId);
			note = $"mark on item {itemInstanceId} names a {mark.ItemId}, but the id now carries a {itemId}";
			return false;
		}

		if (mark.OwnerSteamId != ownerSteamId)
		{
			note = $"mark on item {itemInstanceId} followed it from {mark.OwnerSteamId} to {ownerSteamId}";
			mark.OwnerSteamId = ownerSteamId;
		}

		return true;
	}

	/// <summary>
	/// The gesture: flip the mark this item carries. Returns the value now in force —
	/// the value the marker's own view shows from this frame on. Only the local player's
	/// own view is affected; nothing about the item or its owner changes anywhere.
	/// <para>
	/// The flip is decided by the same question the view asks (<see cref="IsMarked"/>),
	/// not by whether an entry happens to exist: an entry that records a DIFFERENT item
	/// under this id is not "this item is marked", so pressing the key marks the item
	/// the player is actually looking at instead of silently clearing a stale row.
	/// </para>
	/// </summary>
	internal bool Toggle(ulong itemInstanceId, ulong ownerSteamId, string itemId)
	{
		if (itemInstanceId == 0)
		{
			return false;
		}

		if (IsMarked(itemInstanceId, ownerSteamId, itemId, out _))
		{
			_marks.Remove(itemInstanceId);
			return false;
		}

		_marks[itemInstanceId] = new CharacterFavouriteMarkMsg
		{
			OwnerSteamId = ownerSteamId,
			ItemInstanceId = itemInstanceId,
			ItemId = itemId,
		};

		return true;
	}

	/// <summary>
	/// The marked item became the local player's OWN: the mark is spent, and the
	/// caller writes the value onto the real item so it is that item's own fact
	/// from then on. Matched on the definition as well, because an id that now
	/// carries another item must never be adopted as if it were the marked one.
	/// </summary>
	internal bool TrySpend(ulong itemInstanceId, string itemId)
	{
		if (itemInstanceId == 0 || !_marks.TryGetValue(itemInstanceId, out var mark) || mark.ItemId != itemId)
		{
			return false;
		}

		_marks.Remove(itemInstanceId);
		return true;
	}

	/// <summary>A copy of the marks for the character record this client is about to report — the ledger's own set is never handed out.</summary>
	internal List<CharacterFavouriteMarkMsg> Snapshot()
	{
		var copy = new List<CharacterFavouriteMarkMsg>(_marks.Count);
		foreach (var mark in _marks.Values)
		{
			copy.Add(new CharacterFavouriteMarkMsg
			{
				OwnerSteamId = mark.OwnerSteamId,
				ItemInstanceId = mark.ItemInstanceId,
				ItemId = mark.ItemId,
			});
		}

		return copy;
	}

	/// <summary>
	/// This player's character came back (a reconnect hand-over, a continue, a
	/// respawn): the record's marks REPLACE the live set. Replace rather than
	/// merge, because the record is the whole truth for THIS world — a mark the
	/// record does not carry belongs to a world this client has left, and keeping
	/// it is exactly the stale key that must never be applied.
	/// </summary>
	internal void Seed(IReadOnlyList<CharacterFavouriteMarkMsg>? marks)
	{
		_marks.Clear();
		if (marks is null)
		{
			return;
		}

		foreach (var mark in marks)
		{
			if (mark.ItemInstanceId != 0)
			{
				_marks[mark.ItemInstanceId] = mark;
			}
		}
	}

	/// <summary>The session ended or a new run started: the marks are world-scoped, so the next world's character record is what seeds the next set.</summary>
	internal void Reset() => _marks.Clear();
}
