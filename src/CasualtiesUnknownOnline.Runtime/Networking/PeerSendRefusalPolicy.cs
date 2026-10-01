using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// Pure per-peer send-refusal policy: what the transport does when Steam
/// refuses to queue a message. Two behaviours, both owned here.
///
/// <para>
/// <b>Congestion</b> (<c>k_EResultLimitExceeded</c> — the peer's queue is
/// full, so sending more right now is pointless): the peer is gated. Further
/// attempts are suppressed until a doubling delay elapses (250 ms → 2 s), one
/// attempt is then allowed, and a success clears the episode. The first refusal
/// of an episode, one aggregate per report window while it lasts, its recovery,
/// and the stall escalation (every attempt refused for 30 s) are the only
/// observations the caller is asked to log — a refused send can never produce
/// one log line per attempt, which is the defect this policy removes.
/// </para>
///
/// <para>
/// <b>Every other failure kind</b> (dead session, bad certificate, rendezvous,
/// timeout): the attempt is not gated — sending is how the transport re-drives
/// a broken session and the warm-up pump owns that backoff — but repeated
/// failures still aggregate into one line per report window instead of one per
/// attempt.
/// </para>
///
/// <para>
/// A pure decision machine, like <see cref="PeerWarmupBackoff"/>: all time comes
/// in from the caller (<c>ITimeSource.NowMs</c>), there is no wall clock and no
/// transport dependency, and it is main-thread only. Two lapses keep it
/// session-clean without a timer of its own: an episode whose last refusal is
/// older than the stall bound restarts fresh instead of escalating, and
/// <see cref="Reset"/> drops everything on a session edge (the watchdog calls it
/// when the session ends) so no report window counts the previous session's
/// suppressions. A backwards clock jump — <c>Environment.TickCount</c> wraps
/// after ~24.9 days of machine uptime — is read the same way: the stored
/// deadlines belong to the old epoch, so they are dropped rather than turned
/// into an enormous remaining delay.
/// </para>
/// </summary>
internal sealed class PeerSendRefusalPolicy
{
	internal const long DefaultBaseDelayMs = 250;
	internal const long DefaultMaxDelayMs = 2_000;
	internal const long DefaultReportIntervalMs = 5_000;
	internal const long DefaultStallMs = 30_000;

	private static readonly SendRefusalReport[] NoReports = [];
	private static readonly SendStallReport[] NoStalls = [];

	private readonly long _baseDelayMs;
	private readonly long _maxDelayMs;
	private readonly long _reportIntervalMs;
	private readonly long _stallMs;
	private readonly Dictionary<ulong, PeerState> _peers = [];
	private long _lastNowMs;

	internal PeerSendRefusalPolicy(
		long baseDelayMs = DefaultBaseDelayMs,
		long maxDelayMs = DefaultMaxDelayMs,
		long reportIntervalMs = DefaultReportIntervalMs,
		long stallMs = DefaultStallMs)
	{
		_baseDelayMs = Math.Max(1, baseDelayMs);
		_maxDelayMs = Math.Max(_baseDelayMs, maxDelayMs);
		_reportIntervalMs = Math.Max(1, reportIntervalMs);
		_stallMs = Math.Max(_reportIntervalMs, stallMs);
	}

	/// <summary>
	/// Asks whether the peer may be sent to right now. A false answer IS a
	/// suppressed attempt: it is counted into the next aggregate report, and the
	/// caller must return without touching Steam.
	/// </summary>
	internal bool TryBeginSend(ulong peerId, long nowMs)
	{
		RebaseOnBackwardsClock(nowMs);

		if (!_peers.TryGetValue(peerId, out var state)
			|| state.CongestedSinceMs < 0
			|| nowMs >= state.NextAttemptMs)
		{
			return true;
		}

		state.SuppressedAttempts++;
		state.SuppressedSinceCongestion++;
		EnsureReportWindow(state, nowMs);
		return false;
	}

	/// <summary>
	/// Records one failed send. Returns true when the caller should write the
	/// full diagnostic line: the first failure of a report window, or the onset
	/// of a congestion episode. Every later failure in the window is only
	/// counted.
	/// </summary>
	internal bool RecordFailure(ulong peerId, SteamSendFailureKind kind, long nowMs)
	{
		RebaseOnBackwardsClock(nowMs);

		var state = GetOrCreate(peerId);
		var firstInWindow = state.RefusedAttempts == 0 && state.SuppressedAttempts == 0;
		EnsureReportWindow(state, nowMs);

		var congestedOnset = false;
		if (kind == SteamSendFailureKind.QueueFull)
		{
			// A fresh episode starts when the peer was not congested, or when the
			// last refusal is already older than the stall bound — an episode that
			// lapsed must not make one new refusal look like 30 s of refusals.
			var episodeLapsed = state.CongestedSinceMs < 0 || nowMs - state.LastRefusalMs > _stallMs;
			if (episodeLapsed)
			{
				state.CongestedSinceMs = nowMs;
				state.BackoffMs = _baseDelayMs;
				state.RefusedSinceCongestion = 0;
				state.SuppressedSinceCongestion = 0;
				state.StallReported = false;
				congestedOnset = true;
			}
			else
			{
				state.BackoffMs = Math.Min(_maxDelayMs, state.BackoffMs * 2);
			}

			state.RefusedSinceCongestion++;
			state.NextAttemptMs = nowMs + state.BackoffMs;
		}

		state.RefusedAttempts++;
		state.LastRefusalMs = nowMs;
		return firstInWindow || congestedOnset;
	}

	/// <summary>
	/// Records a send that left the transport. Returns a recovery report when
	/// this success ended a congestion episode — the caller logs it once and
	/// never repeats it.
	/// </summary>
	internal SendRecoveryReport? RecordSuccess(ulong peerId, long nowMs)
	{
		RebaseOnBackwardsClock(nowMs);

		if (!_peers.TryGetValue(peerId, out var state) || state.CongestedSinceMs < 0)
		{
			return null;
		}

		var report = new SendRecoveryReport(
			peerId,
			nowMs - state.CongestedSinceMs,
			state.RefusedSinceCongestion,
			state.SuppressedSinceCongestion);

		state.CongestedSinceMs = -1;
		state.BackoffMs = 0;
		state.NextAttemptMs = 0;
		state.StallReported = false;
		state.RefusedAttempts = 0;
		state.SuppressedAttempts = 0;
		state.ReportWindowStartMs = -1;
		return report;
	}

	/// <summary>
	/// The aggregate reports due now: at most one per peer per report interval,
	/// and only when something was refused or suppressed since the last one.
	/// </summary>
	internal IReadOnlyList<SendRefusalReport> CollectDueReports(long nowMs)
	{
		RebaseOnBackwardsClock(nowMs);

		List<SendRefusalReport>? due = null;
		foreach (var pair in _peers)
		{
			var state = pair.Value;
			if (state.ReportWindowStartMs < 0
				|| nowMs - state.ReportWindowStartMs < _reportIntervalMs
				|| (state.RefusedAttempts == 0 && state.SuppressedAttempts == 0))
			{
				continue;
			}

			(due ??= []).Add(new SendRefusalReport(
				pair.Key,
				state.RefusedAttempts,
				state.SuppressedAttempts,
				nowMs - state.ReportWindowStartMs,
				state.CongestedSinceMs < 0 ? 0 : nowMs - state.CongestedSinceMs));

			state.RefusedAttempts = 0;
			state.SuppressedAttempts = 0;
			state.ReportWindowStartMs = -1;
		}

		return due is null ? NoReports : due;
	}

	/// <summary>
	/// The peers whose congestion has lasted past the stall bound, each reported
	/// once per episode. An episode whose refusals lapsed beyond the stall bound
	/// is not escalated: nothing has been attempted for that long, so the peer is
	/// not "still refusing" — it is simply idle.
	/// </summary>
	internal IReadOnlyList<SendStallReport> CollectStallEscalations(long nowMs)
	{
		RebaseOnBackwardsClock(nowMs);

		List<SendStallReport>? stalled = null;
		foreach (var pair in _peers)
		{
			var state = pair.Value;
			if (state.CongestedSinceMs < 0
				|| state.StallReported
				|| nowMs - state.CongestedSinceMs < _stallMs
				|| nowMs - state.LastRefusalMs > _stallMs)
			{
				continue;
			}

			state.StallReported = true;
			(stalled ??= []).Add(new SendStallReport(
				pair.Key,
				nowMs - state.CongestedSinceMs,
				state.RefusedSinceCongestion));
		}

		return stalled is null ? NoStalls : stalled;
	}

	/// <summary>The session/lobby changed (or the clock jumped backwards): no refusal episode,
	/// report window or suppression count crosses into the next one.</summary>
	internal void Reset() => _peers.Clear();

	/// <summary>
	/// Drops every stored deadline and episode stamp when the caller's clock moved
	/// backwards. <c>Environment.TickCount</c> wraps after ~24.9 days of machine
	/// uptime, and a deadline written before the wrap would otherwise read as an
	/// enormous remaining delay (gated forever, with the report and stall guards
	/// turning negative). Rebuilding the state from the new epoch is the only
	/// reading that keeps every later comparison honest.
	/// </summary>
	private void RebaseOnBackwardsClock(long nowMs)
	{
		if (nowMs < _lastNowMs)
		{
			_peers.Clear();
		}

		_lastNowMs = nowMs;
	}

	private static void EnsureReportWindow(PeerState state, long nowMs)
	{
		if (state.ReportWindowStartMs < 0)
		{
			state.ReportWindowStartMs = nowMs;
		}
	}

	private PeerState GetOrCreate(ulong peerId)
	{
		if (!_peers.TryGetValue(peerId, out var state))
		{
			state = new PeerState();
			_peers[peerId] = state;
		}

		return state;
	}

	/// <summary>One peer's refusal episode, report window and gate state.</summary>
	private sealed class PeerState
	{
		public long CongestedSinceMs = -1;
		public long NextAttemptMs;
		public long BackoffMs;
		public long LastRefusalMs;
		public long ReportWindowStartMs = -1;
		public int RefusedAttempts;
		public int SuppressedAttempts;
		public int RefusedSinceCongestion;
		public int SuppressedSinceCongestion;
		public bool StallReported;
	}

	/// <summary>One aggregate line's worth of refusal evidence for one peer.</summary>
	internal readonly record struct SendRefusalReport(
		ulong PeerId,
		int RefusedAttempts,
		int SuppressedAttempts,
		long WindowMs,
		long CongestedForMs);

	/// <summary>A congestion episode that ended with a successful send.</summary>
	internal readonly record struct SendRecoveryReport(
		ulong PeerId,
		long CongestedForMs,
		int RefusedAttempts,
		int SuppressedAttempts);

	/// <summary>A congestion episode that has lasted past the stall bound.</summary>
	internal readonly record struct SendStallReport(
		ulong PeerId,
		long CongestedForMs,
		int RefusedAttempts);
}
