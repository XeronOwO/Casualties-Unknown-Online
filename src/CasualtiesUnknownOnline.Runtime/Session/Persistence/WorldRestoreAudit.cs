using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

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
/// A restore therefore OWES one contribution per live-world half it will have
/// (<see cref="WorldRestoreHalf"/>): the world facts and the adapter's native
/// handover (one half, reported as one), the restored world-entity facts when that
/// arm is armed, and the item reconcile on a mid-run cut.
/// <see cref="BeginRestore"/> is handed exactly the halves the click ARMED, so a
/// layer-end cut owes one less. The report is raised once — when every owed half
/// has arrived — so a consumer never reads a half-written restore as a completed
/// one, and a half is accounted at most ONCE: several release paths can drop the
/// same arm (the session ending, a supersession, a throw at the seam), and the
/// identity is what keeps the second release from standing in for a half this
/// restore still owes. A half that will never arrive is accounted for explicitly
/// (<see cref="LiveWriteAbandoned"/>) rather than silently dropping the
/// expectation.
///
/// An account is opened for ONE restore ATTEMPT, and every contribution echoes the
/// attempt it belongs to: the writers stamp the kernel restore sequence onto the arm
/// they take it from, <see cref="BeginRestore"/> opens the account with the same
/// value, and a half of any other attempt is ignored rather than counted
/// (<see cref="LiveWriteFinished"/>). Without that identity a half of the previous
/// restore could stand in for one the new restore still owes and raise its report
/// early — the account is reopened by the next restore, not closed by it.
///
/// Both halves are Runtime state, so this audit is a plain service — the adapter
/// only calls it (Begin at the click, the contribution calls at the seam) and
/// subscribes to the report.
/// </summary>
public sealed class WorldRestoreAudit(ILogger<WorldRestoreAudit>? log = null)
{
	private readonly List<WorldRestoreLiveWriteReport> _contributions = [];
	private readonly List<WorldRestoreHalf> _halves = [];
	private readonly HashSet<WorldRestoreHalf> _accounted = [];
	private string _worldId = string.Empty;
	private ulong _restoreSequence;
	private bool _awaiting;
	private bool _closed;

	/// <summary>The last COMPLETE account of a restore's live-world halves, or null before the first one.</summary>
	public WorldRestoreLiveWriteReport? Last { get; private set; }

	/// <summary>How many live-world halves have reported for the restore in flight (diagnostics and tests).</summary>
	public int Contributions => _contributions.Count;

	/// <summary>The halves the restore in flight owes, in the order <see cref="BeginRestore"/> was handed them.</summary>
	public IReadOnlyList<WorldRestoreHalf> ExpectedHalves => _halves;

	/// <summary>True = a restore applied at the click is still waiting for one or more of its live-world halves.</summary>
	public bool AwaitingLiveWrite => _awaiting;

	/// <summary>Raised once per restore, when its last live-world half reported, complete or not.</summary>
	public event Action<WorldRestoreLiveWriteReport>? Reported;

	/// <summary>
	/// The Continue click applied the cut: from here on the live-world halves are
	/// expected. <paramref name="halves"/> is what the restore actually armed (see
	/// <c>WorldRestoreApplier.LiveWorldHalves</c>) — the world-fact half is the one
	/// every restore owes, and it reports even when the cut carried no fact at all,
	/// so an empty list means that half alone. Resets <see cref="Last"/>, because a
	/// completed restore's report must never be read as the new one's outcome, and
	/// reopens the account a previous report or abandonment closed.
	///
	/// <paramref name="restoreSequence"/> is the ATTEMPT this account is for (the
	/// kernel restore that produced the restored state). Every writer stamps the same
	/// value onto the arm it creates, and every contribution echoes it, so a write
	/// that belongs to an earlier attempt is not counted here (see
	/// <see cref="LiveWriteFinished"/>) — without it a straggler could stand in for a
	/// half this restore still owes and raise the report early.
	/// </summary>
	public void BeginRestore(string worldId, ulong restoreSequence, IReadOnlyList<WorldRestoreHalf> halves)
	{
		_worldId = worldId;
		_restoreSequence = restoreSequence;
		_awaiting = true;
		_closed = false;
		_halves.Clear();
		foreach (var half in halves)
		{
			// Deduplicated on entry: completion compares the ACCOUNTED halves against the
			// OWED ones, so a list naming one half twice would owe a contribution that can
			// only ever arrive once and the account could never complete.
			if (!_halves.Contains(half))
			{
				_halves.Add(half);
			}
		}

		if (_halves.Count == 0)
		{
			_halves.Add(WorldRestoreHalf.WorldFacts);
		}

		_accounted.Clear();
		_contributions.Clear();
		Last = null;
	}

	/// <summary>
	/// One live-world half finished writing. The restore's report is raised only
	/// when the LAST owed half arrives, and it merges every half's account: complete
	/// only if all of them were.
	///
	/// A restore reports ONCE, and an abandoned attempt reports nothing: a
	/// contribution that arrives after either is ignored, because the next
	/// generation's world-entry seam runs the same replay call and reports a no-op
	/// when nothing is pending — a completed restore must not turn that into a
	/// second report for the world it already reported, and a dead attempt must not
	/// invent one from a handover it left behind.
	///
	/// <paramref name="half"/> is WHICH of the owed halves reported. It is what makes
	/// a half this account does not owe — or a half that already reported, which is what
	/// a legitimate second release of the same arm looks like — a contribution that is
	/// dropped rather than one that completes the account in place of the half still
	/// missing. <paramref name="restoreSequence"/>
	/// is the ATTEMPT this half belongs to — the value its writer stamped on the arm
	/// when it took it. While an account is open that value is the only thing that
	/// makes the contribution attributable: a half of an EARLIER attempt can still be
	/// in flight when a new restore opens its own account (<see cref="BeginRestore"/>
	/// REOPENS the account, it does not close it), and counting it would let it stand
	/// in for a half the new restore still owes — raising that restore's report before
	/// its own writers ran, with the previous world's outcome in it. A mismatched half
	/// is therefore IGNORED and logged, which is the same producer-bug verdict an
	/// unowed half gets. Only an OPEN account enforces the identity: a contribution
	/// that arrives with no account at all still reports itself (the documented path
	/// where a write reaches the seam without the click), and that report CLOSES the
	/// idle account — so the first such write reports and a later one is dropped until
	/// a <see cref="BeginRestore"/> opens the account again. Every production writer is
	/// per-attempt, so the limit is a property of the diagnostic path, not of a restore.
	/// </summary>
	public void LiveWriteFinished(WorldRestoreHalf half, ulong restoreSequence, bool complete, IReadOnlyList<string> refused, string summary)
	{
		if (_closed)
		{
			return;
		}

		if (_awaiting)
		{
			if (restoreSequence != _restoreSequence)
			{
				log?.LogWarning(
					"[Restore] the {Half} half of restore {Sequence} reached the account open for restore {Open} (world {WorldId}); it is NOT counted toward it: {Summary}",
					half, restoreSequence, _restoreSequence, _worldId, summary);
				return;
			}

			if (!_halves.Contains(half))
			{
				log?.LogWarning(
					"[Restore] the {Half} half reported for restore {Sequence} (world {WorldId}), which owes no such half; it is NOT counted: {Summary}",
					half, restoreSequence, _worldId, summary);
				return;
			}

			if (!_accounted.Add(half))
			{
				// EXPECTED on a legitimate path, not a producer bug: the armed half's owner
				// releases it after the world-entry seam already reported it (the replay
				// reports the half and then ends the arm), and the session teardown may
				// release it once more. The half counts once, and the later release is named
				// only at debug level — the loud verdicts are the unowed half and the
				// mismatched attempt above.
				log?.LogDebug(
					"[Restore] the {Half} half of restore {Sequence} (world {WorldId}) already reported; the later release is NOT counted: {Summary}",
					half, restoreSequence, _worldId, summary);
				return;
			}

			log?.LogDebug(
				"[Restore] the {Half} half reported for restore {Sequence} (world {WorldId}, complete={Complete}): {Summary}",
				half, restoreSequence, _worldId, complete, summary);
			_contributions.Add(new WorldRestoreLiveWriteReport(_worldId, complete, refused, summary));
			if (_accounted.Count < _halves.Count)
			{
				return;
			}
		}
		else
		{
			log?.LogDebug(
				"[Restore] a live-world half reported with no restore in flight (complete={Complete}): {Summary}",
				complete, summary);
			_contributions.Add(new WorldRestoreLiveWriteReport(_worldId, complete, refused, summary));
		}

		var report = Merge(_contributions);
		Last = report;
		_awaiting = false;
		_closed = true;
		Reported?.Invoke(report);
	}

	/// <summary>
	/// A live-world half the restore was waiting for will never arrive (the run was
	/// superseded, the session ended, the generation reconcile was cancelled, the
	/// write threw). It is accounted as an incomplete contribution — never silently
	/// dropped — and completes the restore's report when it was the last one
	/// outstanding. A call with no restore in flight is a no-op (a layer-end cut's own
	/// cancellation), and a release of an arm that already reported, or of one the
	/// account does not owe, is not counted: a dead attempt's release is not this
	/// restore's half, and the same armed half can be released along more than one
	/// path.
	/// </summary>
	public void LiveWriteAbandoned(WorldRestoreHalf half, ulong restoreSequence, string reason)
	{
		if (!_awaiting)
		{
			return;
		}

		LiveWriteFinished(
			half,
			restoreSequence,
			complete: false,
			refused: [reason],
			summary: $"the restored {Describe(half)} half never reached the live world: {reason}");
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
		_halves.Clear();
		_accounted.Clear();
		_worldId = string.Empty;
		_contributions.Clear();
		Last = null;
	}

	/// <summary>The half's name as the restore report and the log name it.</summary>
	private static string Describe(WorldRestoreHalf half) => half switch
	{
		WorldRestoreHalf.WorldEntities => "world-entity",
		WorldRestoreHalf.WorldItems => "item",
		_ => "world-fact",
	};

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
