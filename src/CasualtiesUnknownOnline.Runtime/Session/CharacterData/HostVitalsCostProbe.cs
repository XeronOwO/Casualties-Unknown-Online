using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The host vitals pass's frame-cost readout: one window aggregates every remote clone's step
/// timings and emits a single Debug line, so the ticket's affordability question — what N
/// simulated clones cost per fixed step on the host — is answered from a real session instead of
/// a guess. Debug on purpose: the line is diagnostic, a run raises <c>Logging.MinimumLevel</c> to
/// capture it, and normal play pays one filtered check per step.
///
/// <para>
/// Two durations are recorded per step: the WHOLE step (snapshot read, gates, input scope, the
/// <see cref="HostVitalsPass"/> call, write-back) and the pass itself — the game's five stages plus
/// CUO's pass-level wrapper (RNG and camera-field preservation, the quarantine, reflection
/// invocations). The step-minus-pass difference is CUO's body-level bookkeeping. The window line
/// reports per-clone averages and maxima in microseconds, and converts the total step time per
/// wall second into two readings: <c>frameCost</c>, the cost of ONE fixed-step invocation across
/// every body that ran (the ticket's "N bodies × the pass per fixed step"), and
/// <c>frameBudget</c>, that cost as a share of the host's wall time. The fixed step is game time,
/// so the line carries <c>timeScale</c>: at 25× a FixedUpdate runs 25 times per wall second, the
/// per-invocation cost stays the same and the wall share rises 25×. A window re-opens whenever
/// the fixed step or the time scale changes, and a pause above <see cref="IdleResetSeconds"/>
/// emits the partial window before restarting, so no window averages two bases or a pause.
/// </para>
///
/// <para>
/// Called from the fixed step on the main thread (Unity dispatches every clone's
/// <c>FixedUpdate</c> there), so the window needs no locking. While Debug is filtered the caller
/// calls <see cref="Suspend"/> instead of <see cref="Record"/>: the window closes, and turning
/// Debug on later starts a fresh one.
/// </para>
/// </summary>
internal sealed class HostVitalsCostProbe
{
	/// <summary>The summary interval: long enough to cover an in-game action, short enough to watch while playing.</summary>
	internal const float WindowSeconds = 10f;

	/// <summary>A longer gap between steps emits the partial window and restarts: paused time is not model cost.</summary>
	internal const float IdleResetSeconds = 1f;

	private readonly ILogger _log;
	private readonly long _ticksPerSecond;
	private readonly long _windowTicks;
	private readonly long _idleResetTicks;
	private readonly HashSet<ulong> _bodies = [];

	private bool _open;
	private long _windowStartTicks;
	private long _lastRecordTicks;
	private long _stepTicks;
	private long _stepMaxTicks;
	private long _passTicks;
	private long _passMaxTicks;
	private int _steps;
	private int _passes;
	private float _fixedStepSeconds;
	private float _timeScale;

	internal HostVitalsCostProbe(ILogger log)
		: this(log, Stopwatch.Frequency)
	{
	}

	/// <summary>Test seam: a known tick rate makes the window boundary and the printed averages deterministic.</summary>
	internal HostVitalsCostProbe(ILogger log, long ticksPerSecond)
	{
		if (log is null)
		{
			throw new ArgumentNullException(nameof(log));
		}

		if (ticksPerSecond <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), ticksPerSecond, "A tick rate must be positive.");
		}

		_log = log;
		_ticksPerSecond = ticksPerSecond;
		_windowTicks = (long)(WindowSeconds * ticksPerSecond);
		_idleResetTicks = (long)(IdleResetSeconds * ticksPerSecond);
	}

	/// <summary>True while the summary is enabled; the caller skips its stopwatches when this is false.</summary>
	internal bool Enabled => _log.IsEnabled(LogLevel.Debug);

	/// <summary>Report a filtered step: the next enabled step starts a fresh window instead of averaging the gap in.</summary>
	internal void Suspend() => _open = false;

	/// <summary>
	/// Record one completed step. <paramref name="nowTicks"/> and <paramref name="stepStartTicks"/>
	/// come from <see cref="Stopwatch.GetTimestamp"/>; <paramref name="passTicks"/> is the measured
	/// native pass inside that step, or 0 when the step returned before the pass ran. The summary's
	/// own formatting happens after the step was measured, so it is not attributed to any clone.
	/// </summary>
	internal void Record(long nowTicks, long stepStartTicks, long passTicks, ulong steamId, float fixedStepSeconds, float timeScale)
	{
		if (!_log.IsEnabled(LogLevel.Debug))
		{
			_open = false; // a caller that records a filtered step must not leave a stale window behind
			return;
		}

		if (!_open)
		{
			OpenWindow(nowTicks, fixedStepSeconds, timeScale);
		}
		else if (fixedStepSeconds != _fixedStepSeconds
			|| timeScale != _timeScale
			|| nowTicks - _lastRecordTicks > _idleResetTicks)
		{
			// A basis change (the game's fixed delta or a speed change) or a pause: report the window
			// that just ended on its OWN basis, then start over. Averaging across either would make
			// frameCost and frameBudget describe a frame that never existed.
			Emit(_lastRecordTicks);
			OpenWindow(nowTicks, fixedStepSeconds, timeScale);
		}

		_lastRecordTicks = nowTicks;
		_steps++;
		_bodies.Add(steamId);
		var stepTicks = Math.Max(0, nowTicks - stepStartTicks);
		_stepTicks += stepTicks;
		if (stepTicks > _stepMaxTicks)
		{
			_stepMaxTicks = stepTicks;
		}

		if (passTicks > 0)
		{
			_passes++;
			_passTicks += passTicks;
			if (passTicks > _passMaxTicks)
			{
				_passMaxTicks = passTicks;
			}
		}

		if (nowTicks - _windowStartTicks < _windowTicks)
		{
			return;
		}

		Emit(nowTicks);
		OpenWindow(nowTicks, fixedStepSeconds, timeScale);
	}

	/// <summary>
	/// One Debug line per window. <c>frameCost</c> is the cost of one fixed-step invocation across
	/// every body, derived from the observed wall rate and the window's own fixed step/time scale;
	/// <c>frameBudget</c> is the share of wall time those invocations took. At timeScale 1 they are
	/// the familiar per-frame/per-budget pair; at a faster game the per-invocation cost is stable
	/// and the wall share grows with the invocation rate.
	/// </summary>
	private void Emit(long nowTicks)
	{
		var elapsedSeconds = (nowTicks - _windowStartTicks) / (double)_ticksPerSecond;
		if (elapsedSeconds <= 0 || _steps == 0)
		{
			return;
		}

		var stepAvgUs = _stepTicks / (double)_steps / _ticksPerSecond * 1_000_000.0;
		var stepMaxUs = _stepMaxTicks / (double)_ticksPerSecond * 1_000_000.0;
		var passAvgUs = _passes > 0 ? _passTicks / (double)_passes / _ticksPerSecond * 1_000_000.0 : 0.0;
		var passMaxUs = _passMaxTicks / (double)_ticksPerSecond * 1_000_000.0;
		var stepSecondsPerSecond = _stepTicks / (double)_ticksPerSecond / elapsedSeconds;
		// FixedUpdate runs timeScale / fixedDeltaTime times per wall second. Dividing the wall rate
		// by that invocation rate gives the cost of ONE invocation, independent of the game speed.
		var fixedStepsPerSecond = _timeScale > 0f && _fixedStepSeconds > 0f
			? _timeScale / (double)_fixedStepSeconds
			: 0.0;
		var frameCostMs = fixedStepsPerSecond > 0.0 ? stepSecondsPerSecond / fixedStepsPerSecond * 1000.0 : 0.0;
		var frameBudgetPercent = stepSecondsPerSecond * 100.0;

		_log.LogDebug(
			"[Vitals] cost {Window}s: bodies={Bodies} steps={Steps} passes={Passes} stepAvg={StepAvg}us stepMax={StepMax}us passAvg={PassAvg}us passMax={PassMax}us frameCost={FrameCost}ms frameBudget={FrameBudget}% fixedStep={FixedStep}ms timeScale={TimeScale}",
			Number(elapsedSeconds, "F1"),
			_bodies.Count,
			_steps,
			_passes,
			Number(stepAvgUs, "F1"),
			Number(stepMaxUs, "F1"),
			Number(passAvgUs, "F1"),
			Number(passMaxUs, "F1"),
			Number(frameCostMs, "F3"),
			Number(frameBudgetPercent, "F2"),
			Number(_fixedStepSeconds * 1000.0, "F2"),
			Number(_timeScale, "0.###"));
	}

	private void OpenWindow(long nowTicks, float fixedStepSeconds, float timeScale)
	{
		_open = true;
		_windowStartTicks = nowTicks;
		_lastRecordTicks = nowTicks;
		_steps = 0;
		_passes = 0;
		_bodies.Clear();
		_stepTicks = 0;
		_stepMaxTicks = 0;
		_passTicks = 0;
		_passMaxTicks = 0;
		_fixedStepSeconds = fixedStepSeconds;
		_timeScale = timeScale;
	}

	private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}
