namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cut policy's read-only probe of the open operation sessions: how many
/// medical, shrapnel and other-medical sessions this side holds at the cut
/// instant. It lives beside the session dictionaries (the owner answers about
/// its own state) instead of in the transient table, and it is a QUERY — the
/// session sets themselves stay private to the services that mutate them.
/// </summary>
/// <param name="Medical">Open injection/removal sessions.</param>
/// <param name="Shrapnel">Open shared shrapnel-removal sessions.</param>
/// <param name="Other">Open Stage-3 sessions (bandage/splint/…).</param>
public readonly record struct MedicalSessionCutCounts(int Medical, int Shrapnel, int Other);
