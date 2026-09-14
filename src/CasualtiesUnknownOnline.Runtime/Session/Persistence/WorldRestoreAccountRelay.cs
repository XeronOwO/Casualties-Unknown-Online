using System;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The Continue attempt's player-facing account, in one place: what the click
/// resolved, whether an APPLIED attempt is still outstanding, and the single point a
/// surface subscribes to.
///
/// The outstanding state exists because "abandoned" is a fact about an attempt that is
/// still OPEN, not about the world this service currently writes into: an abandonment
/// arrives with nothing in flight whenever a caller releases handovers without a
/// preceding click, and reporting that as an abandonment would tell the player about a
/// continue they never started. An attempt stops being outstanding when the
/// abandonment is reported, when a new run supersedes it (<see cref="Superseded"/>),
/// or when its live-world account closes — the audit's own report, which is the
/// attempt reaching the world it owed its facts to (see
/// <see cref="WorldRestoreAudit.Reported"/>).
///
/// It is its own type rather than three fields on <see cref="WorldSaveService"/>
/// because it is the one part of the save layer that is about what a PLAYER is told
/// rather than about what a snapshot holds, and because the service is at the
/// architecture file-size ceiling.
/// </summary>
internal sealed class WorldRestoreAccountRelay : IDisposable
{
	private readonly Action<WorldRestoreReport> _raise;
	private readonly WorldRestoreAudit? _audit;
	private WorldRestoreReport? _outstanding;

	/// <param name="raise">Where a report goes (the service's <c>RestoreReported</c> event, raised once per report).</param>
	/// <param name="audit">The restore's live-world account, when the composition has one; its report closes the outstanding attempt.</param>
	internal WorldRestoreAccountRelay(Action<WorldRestoreReport> raise, WorldRestoreAudit? audit = null)
	{
		_raise = raise;
		_audit = audit;
		if (_audit is not null)
		{
			_audit.Reported += OnAccountClosed;
		}
	}

	/// <summary>
	/// The click resolved: the attempt's own account is raised, and an APPLIED one stays
	/// outstanding (a refusal applied no state, so there is nothing an abandonment could
	/// end). The report carries the world id the attempt named — never the service's
	/// current one, which belongs to a different attempt after a new run started.
	/// </summary>
	internal void Resolved(WorldRestoreReport report)
	{
		_outstanding = report.Result == WorldRestoreReport.Disposition.Applied ? report : null;
		_raise(report);
	}

	/// <summary>
	/// The attempt will never reach its world-entry seam, so the run does not start. The
	/// second and last word is raised <em>only</em> for an attempt still outstanding; an
	/// abandonment with nothing in flight is the caller's business (it still releases the
	/// handovers) and must not invent a continue the player never started.
	/// </summary>
	internal void Abandoned(string reason)
	{
		if (_outstanding is not { } attempt)
		{
			return;
		}

		_outstanding = null;
		_raise(new WorldRestoreReport(attempt.WorldId, WorldRestoreReport.Disposition.Abandoned, reason, []));
	}

	/// <summary>A new run owns the next generation: whatever the last click applied is no longer an attempt this session owes a word about.</summary>
	internal void Superseded() => _outstanding = null;

	/// <summary>The attempt's live-world account closed (complete or not): it is no longer waiting for anything, so it is no longer outstanding.</summary>
	private void OnAccountClosed(WorldRestoreLiveWriteReport report) => _outstanding = null;

	public void Dispose()
	{
		if (_audit is not null)
		{
			_audit.Reported -= OnAccountClosed;
		}
	}
}
