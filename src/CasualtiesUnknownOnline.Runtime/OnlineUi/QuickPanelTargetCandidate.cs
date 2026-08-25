namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One in-world remote candidate for the standalone player-interaction quick
/// panel. Only the identity and world position are needed for deterministic
/// nearest-target selection; all action eligibility lives in the existing
/// <see cref="OnlineUiMemberRow"/> projection.
/// </summary>
public readonly record struct QuickPanelTargetCandidate(ulong SteamId, float X, float Y);
