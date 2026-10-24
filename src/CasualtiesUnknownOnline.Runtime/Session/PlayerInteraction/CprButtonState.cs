namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// What the wound view's CPR control should show (ticket `remote-medical-cpr.md`, stage 1c). The control exists for one
/// gesture on one patient: start CPR on a body whose heart has stopped, then keep the rhythm going. Everything else — no
/// remote focus, a body whose heart is beating — shows nothing, because a disabled control still invites the viewer (the
/// same ruling the other local-only surfaces were hidden under).
/// </summary>
public enum CprButtonState
{
	/// <summary>No control: no remote focus, no session, or a body that does not need CPR.</summary>
	Hidden = 0,

	/// <summary>The control starts CPR on this patient (bare hands; the operation is the medical session family's).</summary>
	Start = 1,

	/// <summary>The control is one compression of a running CPR session, pressed in rhythm.</summary>
	Compress = 2,
}
