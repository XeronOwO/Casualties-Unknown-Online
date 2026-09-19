using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The HOST's per-sender partial-damage ledger: (sender, block cell) → the
/// damage that sender has cumulatively contributed to that cell, in the game's
/// accumulated units. It is what makes "counted exactly once" possible at all
/// (review/partial-damage-delta-report-overlap): the host's row is a single number
/// it keeps ACCUMULATING, so the only way a later report can be merged without
/// double-counting is to know how much of that cell each sender already put
/// there.
/// <para>
/// Both arrival paths resolve against it and commit what they applied: a live
/// delta carries its sender's cumulative value, the recovery report carries the
/// same value, and <see cref="Resolve"/> turns either into the INCREMENT this
/// host has not seen yet — 0 for a repeat, a stale frame or a duplicate report,
/// which is exactly what keeps a delayed delta from being applied twice. Two
/// senders keep two entries, so their contributions add up instead of the lower
/// one being swallowed by a per-cell maximum.
/// </para>
/// <para>
/// Entries die with the block they describe: a block write on the cell (break,
/// placement, restored block) forgets the cell for every sender, and a new
/// world/layer baseline clears the table. The host's OWN damage needs no entry —
/// it is already in the row and is never re-reported. Bounded: at the cap a new
/// cell is not tracked, and the caller logs the overflow episode once;
/// <see cref="Resolution.Trackable"/> is false there, so the caller still
/// applies the reported contribution (a full ledger degrades to "the whole
/// reported value is new" rather than losing the sender's damage).
/// </para>
/// </summary>
internal sealed class RemoteBlockDamageLedger
{
	internal const int DefaultCap = 65536;

	/// <summary>One sender's cell resolution: the contribution it reported, the increment this host has not accounted for, and whether the cell could be tracked at all.</summary>
	internal readonly record struct Resolution(ulong Sender, int X, int Y, float Contribution, float Increment, bool Trackable);

	private readonly Dictionary<(ulong Sender, int X, int Y), float> _entries = [];
	private readonly int _cap;

	internal RemoteBlockDamageLedger()
		: this(DefaultCap)
	{
	}

	/// <summary>Test seam (InternalsVisibleTo): the cap is a policy constant; tests exercise the overflow with a small bound.</summary>
	internal RemoteBlockDamageLedger(int cap)
	{
		_cap = cap;
	}

	internal int Count => _entries.Count;

	/// <summary>
	/// What this host still owes for one sender's report of one cell, WITHOUT
	/// changing the ledger (the caller commits what it actually applied, through
	/// <see cref="Commit"/>). A sender the ledger has never seen contributes
	/// everything it reports; a sender it holds resolves to the difference, and
	/// a value at or below what it already holds resolves to no increment at all
	/// — the stale frame and the duplicate report are the same case.
	/// </summary>
	internal Resolution Resolve(ulong sender, int x, int y, float contribution)
	{
		var key = (sender, x, y);
		if (_entries.TryGetValue(key, out var tracked))
		{
			var increment = contribution - tracked;
			return new Resolution(sender, x, y, contribution, increment > 0f ? increment : 0f, true);
		}

		return new Resolution(sender, x, y, contribution, contribution > 0f ? contribution : 0f, _entries.Count < _cap);
	}

	/// <summary>Remember what a sender contributed for the cell. Monotone: a lower value never lowers the ledger, so a stale report that arrives late cannot make the next increment too large.</summary>
	internal void Commit(Resolution resolution)
	{
		if (!resolution.Trackable || resolution.Contribution <= 0f)
		{
			return;
		}

		var key = (resolution.Sender, resolution.X, resolution.Y);
		if (_entries.TryGetValue(key, out var tracked) && tracked >= resolution.Contribution)
		{
			return;
		}

		_entries[key] = resolution.Contribution;
	}

	/// <summary>A block write landed on the cell — the block every sender's contribution described is gone. Returns how many sender entries were dropped.</summary>
	internal int Forget(int x, int y)
	{
		List<(ulong Sender, int X, int Y)>? dropped = null;
		foreach (var key in _entries.Keys)
		{
			if (key.X == x && key.Y == y)
			{
				(dropped ??= []).Add(key);
			}
		}

		if (dropped is null)
		{
			return 0;
		}

		foreach (var key in dropped)
		{
			_entries.Remove(key);
		}

		return dropped.Count;
	}

	/// <summary>The world these contributions belonged to is gone (a new world/layer baseline, or the session ended).</summary>
	internal void Clear() => _entries.Clear();
}
