namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The host-descent follow's state machine: the phase above, the anchor its bounded waits are measured from, and the host's own presentation window.
/// </summary>
/// <remarks>
/// A value-level object with no game types: every transition and every timeout question is answered here, so the decision table can be tested without a session
/// (the policy it asks, <c>LayerDescentPolicy</c>, is unit-pinned on its own). The coordinator keeps the parts only it can do — reading the scene, starting the
/// game's regeneration, mapping the result onto the run phase, and the menu-return origin rule — and asks this object what the follow's state is.
/// </remarks>
internal sealed class HostDescentFollow
{
	private DescentPhase _phase = DescentPhase.None;
	private int _acceptedAtMs;
	private bool _hostPending;

	/// <summary>How far the follow got. <see cref="DescentPhase.None"/> means it is not following.</summary>
	internal DescentPhase Phase => _phase;

	/// <summary>Host: this host's own regeneration is under way (the presentation window before the loading screen is visible).</summary>
	internal bool HostPending => _hostPending;

	/// <summary>True from the edge until the follow ends, on either side.</summary>
	internal bool IsActive => _phase != DescentPhase.None;

	/// <summary>Guest: a generation is in flight (the edge was taken and the settle has not happened).</summary>
	internal bool IsGenerationInFlight => _phase is DescentPhase.Accepted or DescentPhase.Launched or DescentPhase.ReleasedEarly;

	/// <summary>Guest: the generation is in flight and can still be released early (the gate may open before it settles).</summary>
	internal bool CanBeReleasedEarly => _phase is DescentPhase.Accepted or DescentPhase.Launched;

	/// <summary>Guest: the edge was accepted — start counting the bounded waits from here.</summary>
	internal void Accept(int nowMs)
	{
		_phase = DescentPhase.Accepted;
		_acceptedAtMs = nowMs;
	}

	/// <summary>Guest: the generation actually started (only meaningful from <see cref="DescentPhase.Accepted"/>).</summary>
	internal void Launched()
	{
		if (_phase == DescentPhase.Accepted)
		{
			_phase = DescentPhase.Launched;
		}
	}

	/// <summary>Guest: the generation settled — the follow now waits for the host's release.</summary>
	internal void WaitForReady() => _phase = DescentPhase.WaitingReady;

	/// <summary>Guest: the host released the gate while the generation was still in flight.</summary>
	internal void ReleaseEarly() => _phase = DescentPhase.ReleasedEarly;

	/// <summary>Ends the follow on either side.</summary>
	internal void End()
	{
		_phase = DescentPhase.None;
		_hostPending = false;
	}

	/// <summary>Host: this host's own regeneration is starting — open the presentation window.</summary>
	internal void BeginHostDescent() => _hostPending = true;

	/// <summary>Host: the gate released (or the run ended) — the presentation window closes with it.</summary>
	internal void CloseHostDescent() => _hostPending = false;

	/// <summary>
	/// Guest: whether one of the follow's bounded waits has run out AND the world state says the follow cannot finish — the abort condition. The world being gone
	/// aborts immediately (a leave or a scene change mid-descent); otherwise the hold and settle windows are the bounds.
	/// </summary>
	internal bool IsAbortDue(bool hasWorld, int nowMs)
	{
		var elapsedMs = nowMs - _acceptedAtMs;
		var holdTimedOut = _phase == DescentPhase.Accepted && elapsedMs > LayerDescentPolicy.HoldTimeoutMs;
		var settleTimedOut = _phase is DescentPhase.Launched or DescentPhase.ReleasedEarly && elapsedMs > LayerDescentPolicy.SettleTimeoutMs;
		return LayerDescentPolicy.ShouldAbort(hasWorld, holdTimedOut, settleTimedOut);
	}

	/// <summary>The abort's one-line reason: the world was lost, the baseline never arrived, or the generation never settled.</summary>
	internal string AbortReason(bool hasWorld) =>
		!hasWorld ? "the world was left (a leave or a scene change) mid-descent"
		: _phase == DescentPhase.Accepted ? "the host's baseline for the new layer never arrived"
		: "the new layer's generation never settled";

	/// <summary>Guest: whether the settle rule applies yet (the generation has launched, so it has something to settle).</summary>
	internal bool HasLaunched => _phase != DescentPhase.Accepted;
}
