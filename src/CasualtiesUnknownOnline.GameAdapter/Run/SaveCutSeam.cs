using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The post-session menu return's frame-end half.
///
/// Session teardown events run inside Steam/UI callbacks, so the intent is
/// recorded by <see cref="RunMenuReturnCoordinator"/> and the scene load happens
/// on the normal pump. This class is the seam that performs it, and it is the
/// same seam every other cut is taken at: the host's deliberate return is itself
/// a mid-run cut now (the world is still fully alive at that moment), so the cut
/// and the leave are one operation — if the transient policy defers the cut, the
/// leave waits a frame instead of walking out of a world whose in-flight state
/// was never captured.
///
/// Order inside the seam is load-bearing: cut first, then leave. Leaving first
/// would destroy the world the cut has to read.
/// </summary>
internal sealed class SaveCutSeam(
	ISessionControl session,
	IWorldSaveControl saves,
	RunCoordinator run,
	RunMenuReturnCoordinator menuReturn,
	BlockBreakPendingState breakPending,
	TrapDropPendingState trapDrops,
	ItemDropState itemDrops,
	ILogger<SaveCutSeam> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldSaveControl _saves = saves;
	private readonly RunCoordinator _run = run;
	private readonly RunMenuReturnCoordinator _menuReturn = menuReturn;
	private readonly BlockBreakPendingState _breakPending = breakPending;
	private readonly TrapDropPendingState _trapDrops = trapDrops;
	private readonly ItemDropState _itemDrops = itemDrops;
	private readonly ILogger<SaveCutSeam> _log = log;

	/// <summary>
	/// The host pump's frame-end point: every domain has finished this frame's
	/// work (the drop/break flushes included), no command batch is in flight and
	/// no kernel revision is half-applied. Both cut triggers — the armed
	/// <c>/save</c> request and the deliberate menu return — are taken here, and
	/// nowhere else.
	/// </summary>
	internal void Update(bool inWorld)
	{
		var frame = Time.frameCount;

		// The menu return first: it supersedes an armed command cut (one snapshot,
		// one leave) and it is the trigger that must not be lost.
		if (FlushMenuReturn(inWorld, frame))
		{
			return;
		}

		TakeArmedCut(frame);
	}

	/// <summary>
	/// True when a pending menu return was handled this frame (the world was left,
	/// or the request was dropped as stale) — the armed cut must then not run a
	/// second time in the same frame.
	/// </summary>
	private bool FlushMenuReturn(bool inWorld, int frame)
	{
		var mode = _menuReturn.Pending;
		if (mode == RunMenuReturnMode.None)
		{
			return false;
		}

		// The world must be leavable: still in it, no new session that makes the
		// teardown stale, and a camera to hand the scene load to.
		if (!inWorld || _session.SessionActive || PlayerCamera.main == null) // Unity object — ==
		{
			_menuReturn.Clear();
			return false;
		}

		if (mode == RunMenuReturnMode.SaveAndMenu)
		{
			// Arm the same seam cut a console /save uses, with the trigger that names
			// the moment: "the host deliberately left the world".
			if (_saves.TryRequestCut(WorldCutReason.MenuReturn, out var refusal))
			{
				var report = _saves.TryCaptureArmedCut(_run.CaptureLocalCharacter(), frame, LiveTransients());
				if (report is { Result: WorldCutResult.Deferred })
				{
					// In-flight state the policy resolves first is still there: the
					// request stays pending and the leave happens on the frame that
					// could take the cut. Debug: the policy already logged the wait once.
					_log.LogDebug("[SaveSeam] the menu-return cut is deferred: {Summary}", report.Summary);
					return true;
				}

				if (report is { Captured: false })
				{
					// Nothing was written (no baseline, an unreadable native table, a
					// failed transaction). Leaving anyway is the old behaviour, and the
					// console/log now says which cut did not happen.
					_log.LogWarning("[SaveSeam] leaving the world without a cut: {Summary}", report.Summary);
				}
			}
			else
			{
				_log.LogWarning("[SaveSeam] leaving the world without a cut: {Refusal}", refusal);
			}
		}

		_menuReturn.Clear();
		_menuReturn.Leave();
		return true;
	}

	private void TakeArmedCut(int frame)
	{
		if (!_saves.HasArmedCut)
		{
			return;
		}

		var report = _saves.TryCaptureArmedCut(_run.CaptureLocalCharacter(), frame, LiveTransients());
		if (report is null or { Result: WorldCutResult.Deferred })
		{
			// A deferral is expected (a break's drops are still forming) and the
			// request stays armed; the log line belongs to the policy, not here.
			return;
		}

		if (report.Captured)
		{
			_log.LogInformation("[SaveSeam] {Summary}", report.Summary);
		}
		else
		{
			_log.LogWarning("[SaveSeam] the cut was refused: {Summary}", report.Summary);
		}
	}

	/// <summary>
	/// The adapter's half of the transient observation: the game-side windows whose
	/// world effect is only reported to the kernel when they resolve. The Runtime
	/// half (the pickup queue, the operation sessions, the deferred creation
	/// reports) is read by the save service itself; the verdicts for both halves
	/// live in <see cref="WorldTransientPolicy"/>.
	/// </summary>
	private IReadOnlyList<WorldTransientCount> LiveTransients() =>
	[
		new(WorldTransientPolicy.BlockBreakPendingKey, _breakPending.Current == BlockBreakPendingState.Phase.Idle ? 0 : 1),
		new(WorldTransientPolicy.TrapDropHoldKey, _trapDrops.Count),
		new(WorldTransientPolicy.DropFlushKey, _itemDrops.Current == ItemDropState.Phase.Idle ? 0 : 1),
	];
}
