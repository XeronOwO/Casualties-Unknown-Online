using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One host instruction applied on THIS client's own body: the sentence the target's console
/// prints, the operator it names, and the facts the sentence was built from.
/// </summary>
public readonly record struct HealResetNotice(ulong OperatorSteamId, LocalBodyResetOutcome Outcome, string Text);
