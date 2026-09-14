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
/// The menu return's frame-end half.
///
/// A deliberate "leave the world" always runs outside the pump — a session
/// teardown event inside a Steam/UI callback, or the game's own
/// <c>PlayerCamera.ToMainMenu</c> — so the intent is recorded by
/// <see cref="RunMenuReturnCoordinator"/> and both the leave and the scene load
/// happen here. This class is the seam that performs it, and it is the same seam
/// every other cut is taken at: the deliberate return is itself a mid-run cut
/// now (the world is still fully alive at that moment), so the cut and the leave
/// are one operation — if the transient policy defers the cut, the leave waits a
/// frame instead of walking out of a world whose in-flight state was never
/// captured.
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
	/// True when a pending menu return was handled this frame — the world was left,
	/// or the request was dropped (nothing left to leave, or a teardown a newer
	/// session superseded). The armed-cut path below still runs after a drop: the
	/// request and the armed cut are independent triggers, and a dropped RETURN must
	/// not swallow a cut the player or the kernel asked for.
	/// </summary>
	private bool FlushMenuReturn(bool inWorld, int frame)
	{
		var mode = _menuReturn.Pending;
		var decision = RunMenuReturnPolicy.DecideFlush(
			mode,
			_menuReturn.Origin,
			inWorld,
			_session.SessionActive,
			PlayerCamera.main != null); // Unity object — ==
		if (decision == RunMenuReturnFlush.None)
		{
			return false;
		}

		if (decision == RunMenuReturnFlush.Clear)
		{
			// The world is not leavable: it is already gone, there is no camera to
			// hand the scene load to, or a session took over the teardown this
			// request belonged to. Named, because the interception upstream refuses
			// to suppress a leave the seam would drop — reaching here means the
			// state moved between the record and this sample.
			_log.LogInformation(
				"[SaveSeam] the pending {Mode} menu return ({Origin}) is dropped: inWorld={InWorld}, sessionActive={SessionActive}, camera={Camera}.",
				mode, _menuReturn.Origin, inWorld, _session.SessionActive, PlayerCamera.main != null);
			_menuReturn.Clear();
			return false;
		}

		if (mode == RunMenuReturnMode.SaveAndMenu)
		{
			// Arm the same seam cut a console /save uses, with the trigger that names
			// the moment: "the player deliberately left the world". A guest never
			// reaches this branch (the policy answers MenuOnly for it, and the save
			// layer refuses a guest anyway), so solo play — which has no session role
			// — takes the same cut a host does.
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

		// Clear only once the leave actually happened: a leave that could not run
		// (the camera vanished between the sample and this call) leaves the request
		// armed, so the leave is retried instead of being consumed.
		if (_menuReturn.Leave())
		{
			_menuReturn.Clear();
		}

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
