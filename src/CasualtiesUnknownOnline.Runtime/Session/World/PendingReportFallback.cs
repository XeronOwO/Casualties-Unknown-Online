using System;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The generic pending-report fallback's window: an unacknowledged local report
/// whose live send was swallowed is re-sent until the owner answers. The window
/// is armed by the first outstanding entry, so the first live report is never
/// duplicated immediately (an entry created while the window is open is re-sent
/// at the window's end — every pending report is idempotent on the receiver).
/// Entries are dropped by the owning domain when the answer arrives, and cleared
/// wholesale when a new world/layer baseline is applied.
///
/// The cadence has TWO phases, and the guest's own world entry is the edge
/// between them. The steady phase is <see cref="IntervalMs"/> — the 60 s family
/// the host's absolute world resends use. The entry phase is the one the lazy-P2P
/// transport makes dangerous: the documented swallow window runs up to ~30 s
/// after a world entry, so a report made inside it whose live send was dropped
/// would otherwise stay invisible on the host until the 60 s mark. The guest's
/// own InWorld report (<see cref="ISessionControl.LocalSceneReported"/>, the same
/// edge <see cref="SessionControlConvergence"/> arms its readiness window on)
/// therefore opens an entry phase in which the step is
/// <see cref="DenseIntervalMs"/> for <see cref="DenseWindowMs"/> — 12 x 5 s, the
/// budget the readiness window's entry half uses.
///
/// The entry phase is anchored by <see cref="Pump"/> on the first frame after the
/// edge and never polled: an edge that came and went between two updates would
/// otherwise be missed. It is bounded by that anchor and not by an answer, which
/// is what makes its worst case the one the ticket measures: a set that first
/// becomes outstanding at the END of the swallow window still gets its first
/// re-send 5 s later, while a set that first becomes outstanding at the end of
/// the ENTRY phase is already ~30 s past the swallow window — its live send is
/// delivered, and the steady step heals a lost one exactly as it did before this
/// phase existed. A stale anchor can never resurrect the phase: it is retired on
/// the first frame whose reading falls before it (<see cref="Environment.TickCount"/>
/// wraps every ~24.9 days), so a wrap can at most fall back to the steady step.
///
/// <see cref="WorldReportFallbackPump"/> is the clock; this class is the cadence
/// policy shared by every pending-report table: the guest's world-block, partial
/// damage and break-drop channels (audit gaps W1/W2, held together by
/// <see cref="GuestReportFallbacks"/>), its runtime entity creations (E3) and its
/// recipe-unlock set (I6). Each owner constructs its own window — the tables fill
/// and drain independently — and every owner lives exactly as long as the session
/// it subscribes to, so the entry edge needs no unsubscribe path.
/// </summary>
internal sealed class PendingReportFallback
{
	/// <summary>The steady re-report cadence — the same 60 s family as the host's absolute world resends.</summary>
	internal const long IntervalMs = 60_000;

	/// <summary>The entry phase's re-report step — the family's 5 s order of magnitude.</summary>
	internal const long DenseIntervalMs = 5_000;

	/// <summary>How long after the guest's own world entry the entry step governs (12 x 5 s, the readiness window's entry budget).</summary>
	internal const long DenseWindowMs = 60_000;

	private readonly ISessionControl _session;

	private long _armedMs;
	private bool _armed;

	/// <summary>An InWorld report was seen and the next <see cref="Pump"/> has not stamped its frame yet.</summary>
	private bool _entryPending;

	/// <summary>The entry phase is open, anchored at <see cref="_entryMs"/> (the frame of the guest's own world entry).</summary>
	private bool _entryOpen;

	private long _entryMs;

	internal PendingReportFallback(ISessionControl session)
	{
		_session = session;
		session.LocalSceneReported += OnLocalSceneReported;
	}

	/// <summary>
	/// One frame of the cadence: arm on the first outstanding entry, re-send once
	/// per window while entries remain. <paramref name="pendingCount"/> is
	/// the owning table's live count and <paramref name="resend"/> its re-send
	/// action (a no-op when the table emptied between the check and the call).
	/// </summary>
	internal void Pump(long nowMs, int pendingCount, Action resend)
	{
		if (_entryPending)
		{
			// The clock this class is given is the ONLY time source it uses, so the
			// edge is stamped here rather than at the event. Stamped before the role
			// and work gates: the entry phase belongs to the entry, not to the first
			// report that happens to follow it, and a guest that is still connecting
			// when it loads the world re-reports its InWorld state on activation
			// (RunCoordinator.OnSessionActivated — it re-reports only while in world with a
			// live local body), which re-anchors the phase.
			_entryOpen = true;
			_entryMs = nowMs;
			_entryPending = false;
		}

		if (_entryOpen && nowMs < _entryMs)
		{
			// The clock reads backwards past the anchor (Environment.TickCount wraps
			// every ~24.9 days). The entry phase can never outlive its own 60 s, so a
			// backward reading means it is over — retiring it here keeps it from
			// reading as "the future" when the counter comes around again.
			_entryOpen = false;
		}

		if (_session.Role != SessionRole.Guest || !_session.SessionActive || pendingCount == 0)
		{
			_armed = false;
			return;
		}

		if (!_armed)
		{
			_armed = true;
			_armedMs = nowMs;
			return;
		}

		if (nowMs < _armedMs)
		{
			// The clock went backwards (Environment.TickCount wraps every ~24.9
			// days) — re-arm at the new reading instead of stalling until the
			// counter catches up with the old one.
			_armedMs = nowMs;
			return;
		}

		var entryPhase = _entryOpen && nowMs - _entryMs < DenseWindowMs;
		if (nowMs - _armedMs < (entryPhase ? DenseIntervalMs : IntervalMs))
		{
			return;
		}

		_armedMs = nowMs;
		resend();
	}

	/// <summary>
	/// The local scene report edge: an InWorld report opens the entry phase, any
	/// other scene state closes it (the phase belongs to the entry that just
	/// happened, and leaving the world ends that entry).
	/// </summary>
	private void OnLocalSceneReported(SceneStateType state)
	{
		_entryPending = state == SceneStateType.InWorld;
		if (!_entryPending)
		{
			_entryOpen = false;
		}
	}

	/// <summary>
	/// The session ended — the next session's first report must start a fresh
	/// window, and a new world entry must open a fresh entry phase.
	/// </summary>
	internal void Reset()
	{
		_armed = false;
		_armedMs = 0;
		_entryPending = false;
		_entryOpen = false;
		_entryMs = 0;
	}
}
