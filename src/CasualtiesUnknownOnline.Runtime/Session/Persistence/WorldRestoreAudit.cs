using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// Carries a restore's LIVE-WORLD halves back to the caller that started it.
///
/// A restore has two halves in time: <c>TryContinue</c> applies the kernel
/// checkpoint and the Runtime fact tables at the Continue click, and the adapter
/// writes the values only a live world can take at the world-entry seam (the
/// block diff, the game's own partial-damage list, the decided native values, and
/// — for a mid-run cut — the restored world items reconciled against the
/// regenerated layer). Each half reports here, so "the restore succeeded" is not
/// decided by the click alone: a row the live world refused makes the restore
/// incomplete, and <see cref="Last"/> / <see cref="Reported"/> are how the caller
/// and the player-facing surface hear about it (§6: no silent loss).
///
/// A restore therefore OWES one contribution per live-world half it will have: one
/// for the world facts and the adapter's native handover (this half reports them
/// together), one for the restored world-entity facts when that arm is armed, and
/// one for the item reconcile on a mid-run cut (<see cref="BeginRestore"/>'s
/// <c>expectedContributions</c> — the count follows the writers ACTUALLY armed, so
/// a layer-end cut owes one less). The report is
/// raised once — when every contribution has arrived — so a consumer never reads
/// a half-written restore as a completed one. A contribution that will never
/// arrive is accounted for explicitly (<see cref="LiveWriteAbandoned"/>) rather
/// than silently dropping the expectation.
///
/// Both halves are Runtime state, so this audit is a plain service — the adapter
/// only calls it (Begin at the click, the contribution calls at the seam) and
/// subscribes to the report.
/// </summary>
public sealed class WorldRestoreAudit
{
	private readonly List<WorldRestoreLiveWriteReport> _contributions = [];
	private string _worldId = string.Empty;
	private bool _awaiting;
	private bool _closed;
	private int _expected = 1;

	/// <summary>The last COMPLETE account of a restore's live-world halves, or null before the first one.</summary>
	public WorldRestoreLiveWriteReport? Last { get; private set; }

	/// <summary>How many live-world halves have reported for the restore in flight (diagnostics and tests).</summary>
	public int Contributions => _contributions.Count;

	/// <summary>How many contributions the restore in flight owes.</summary>
	public int ExpectedContributions => _expected;

	/// <summary>True = a restore applied at the click is still waiting for one or more of its live-world halves.</summary>
	public bool AwaitingLiveWrite => _awaiting;

	/// <summary>Raised once per restore, when its last live-world half reported, complete or not.</summary>
	public event Action<WorldRestoreLiveWriteReport>? Reported;

	/// <summary>
	/// The Continue click applied the cut: from here on the live-world halves are
	/// expected. <paramref name="expectedContributions"/> is how many writers the
	/// restore actually armed (see <c>WorldRestoreApplier.LiveWorldHalves</c>):
	/// one is the world-fact half, which reports even when the cut carried no fact.
	/// Resets <see cref="Last"/>, because a completed restore's report must never be
	/// read as the new one's outcome, and reopens the account a previous report or
	/// abandonment closed.
	/// </summary>
	public void BeginRestore(string worldId, int expectedContributions = 1)
	{
		_worldId = worldId;
		_awaiting = true;
		_closed = false;
		_expected = Math.Max(1, expectedContributions);
		_contributions.Clear();
		Last = null;
	}

	/// <summary>
	/// One live-world half finished writing. The restore's report is raised only
	/// when the LAST expected contribution arrives, and it merges every half's
	/// account: complete only if all of them were.
	///
	/// A restore reports ONCE, and an abandoned attempt reports nothing: a
	/// contribution that arrives after either is ignored, because the next
	/// generation's world-entry seam runs the same replay call and reports a no-op
	/// when nothing is pending — a completed restore must not turn that into a
	/// second report for the world it already reported, and a dead attempt must not
	/// invent one from a handover it left behind. The count a caller passes is the
	/// contract that makes this safe: it names the writers that WILL report, so a
	/// straggler is a producer bug, and inventing a report for it would hide the bug
	/// rather than surface it.
	/// </summary>
	public void LiveWriteFinished(bool complete, IReadOnlyList<string> refused, string summary)
	{
		if (_closed)
		{
			return;
		}

		_contributions.Add(new WorldRestoreLiveWriteReport(_worldId, complete, refused, summary));
		if (_contributions.Count < _expected)
		{
			return;
		}

		var report = Merge(_contributions);
		Last = report;
		_awaiting = false;
		_closed = true;
		Reported?.Invoke(report);
	}

	/// <summary>
	/// A live-world half the restore was waiting for will never arrive (the run was
	/// superseded, the session ended, the generation reconcile was cancelled). It is
	/// accounted as an incomplete contribution — never silently dropped — and
	/// completes the restore's report when it was the last one outstanding. A call
	/// with no restore in flight is a no-op (a layer-end cut's own cancellation).
	/// </summary>
	public void LiveWriteAbandoned(string reason)
	{
		if (!_awaiting)
		{
			return;
		}

		LiveWriteFinished(complete: false, refused: [reason], summary: $"a restored half never reached the live world: {reason}");
	}

	/// <summary>
	/// The restore never reached its world-entry seam (the run was superseded, or
	/// the session ended): the pending expectation is dropped without inventing a
	/// report — a restore that never happened is not a restore that succeeded, and
	/// the previous restore's account must not be read as this one's outcome. The
	/// account is CLOSED (<see cref="_closed"/>) rather than merely un-awaited: a
	/// handover this attempt left armed somewhere must not turn into a report for a
	/// restore that was already abandoned, and the next restore reopens the account
	/// through <see cref="BeginRestore"/>.
	///
	/// With NO restore in flight this is a no-op: nothing is being abandoned, and
	/// closing an idle account would disable the documented path where a write with
	/// no <see cref="BeginRestore"/> still reports itself (a test host, or a future
	/// path that arms a handover without the click).
	/// </summary>
	public void AbandonRestore()
	{
		if (!_awaiting)
		{
			return;
		}

		_awaiting = false;
		_closed = true;
		_expected = 1;
		_worldId = string.Empty;
		_contributions.Clear();
		Last = null;
	}

	private WorldRestoreLiveWriteReport Merge(List<WorldRestoreLiveWriteReport> contributions)
	{
		if (contributions.Count == 1)
		{
			return contributions[0];
		}

		var complete = true;
		var refused = new List<string>();
		foreach (var contribution in contributions)
		{
			complete &= contribution.Complete;
			foreach (var entry in contribution.Refused)
			{
				if (!refused.Contains(entry))
				{
					refused.Add(entry);
				}
			}
		}

		return new WorldRestoreLiveWriteReport(
			_worldId,
			complete,
			refused,
			string.Join("; ", contributions.Select(contribution => contribution.Summary)));
	}
}
