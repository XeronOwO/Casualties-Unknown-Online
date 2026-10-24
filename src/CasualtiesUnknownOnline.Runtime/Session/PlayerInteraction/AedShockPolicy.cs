namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The AED's own advice rule (ticket `remote-medical-cpr.md`, stage 3's second half): a shock is only advised when the
/// heart is fibrillating. That is what makes the shared restart honest — CPR restarts an ARRESTED heart, an AED clears a
/// QUIVERING one, and whichever runs second finds its own condition gone and refuses instead of damaging a patient who no
/// longer needs it. The real device says "no shock advised" and does not discharge; this is the same refusal, and it is
/// the arbitration the ruling asks for because the host applies both operations to the same snapshot in order: the first
/// one changes the state the second one is checked against.
/// </summary>
public static class AedShockPolicy
{
	/// <summary>
	/// Whether this patient's heart warrants a shock: fibrillation is present (the game's `FibrillationProgress`, or the
	/// forced flag its own model sets when the quiver is scripted).
	/// </summary>
	public static bool IsShockAdvised(float fibrillationProgress, bool fibrillationForced) =>
		fibrillationForced || fibrillationProgress > 0f;
}
