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
	/// The answer is yes only when the id is marked AND the live item carries the
	/// marked owner and definition; a mark whose id now names a DIFFERENT item is
	/// DROPPED here and named in <paramref name="staleDrop"/>, so a key the world
	/// has moved on from can never light up on whatever inherited its id.
	/// <para>
	/// The owner is part of that guard on purpose, and the price is deliberate: an item
	/// the mark names that moves to a THIRD player drops the mark, and the local player
	/// has to mark it again. The trade is the acceptance matrix's row 7 against a mark
	/// that follows the id onto a stranger's item. The item that comes to THIS player is
	/// not affected at all — <see cref="TrySpend"/> matches on the definition, so
	/// requirement 3 holds across the hand-off.
	/// </para>
	/// </summary>
	internal bool IsMarked(ulong itemInstanceId, ulong ownerSteamId, string itemId, out string? staleDrop)
	{
		staleDrop = null;
		if (itemInstanceId == 0 || !_marks.TryGetValue(itemInstanceId, out var mark))
		{
			return false;
		}

		if (mark.OwnerSteamId == ownerSteamId && mark.ItemId == itemId)
		{
			return true;
		}

		_marks.Remove(itemInstanceId);
		staleDrop = $"mark on item {itemInstanceId} names {mark.ItemId} of {mark.OwnerSteamId}, but the id now carries {itemId} of {ownerSteamId}";
		return false;
	}

	/// <summary>
	/// The gesture: flip the mark this item carries. Returns the value now in
	/// force — the value the marker's own view shows from this frame on. Only the
	/// local player's own view is affected; nothing about the item or its owner
	/// changes anywhere.
	/// </summary>
	internal bool Toggle(ulong itemInstanceId, ulong ownerSteamId, string itemId)
	{
		if (itemInstanceId == 0)
		{
			return false;
		}

		if (_marks.Remove(itemInstanceId))
		{
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
