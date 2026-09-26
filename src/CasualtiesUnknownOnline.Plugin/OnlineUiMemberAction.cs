using System;

namespace CasualtiesUnknownOnline;

/// <summary>
/// One interaction button of a member card: its stable id, its caption, its width hint and what it does
/// (ticket online-ui-art-and-controls-overhaul, S2b).
///
/// <para>
/// It exists because the card has two renderers during the migration — the model the Players page hands to
/// the game's own controls, and the IMGUI path the quick panel still draws itself until S4 retires it —
/// and "which buttons apply to this member" must be answered once, not twice: the eligibility itself
/// still comes from <see cref="OnlineUiMemberProjection"/>, and this type only carries the answer to
/// whichever renderer is asking.
/// </para>
/// </summary>
internal readonly record struct OnlineUiMemberAction(string Id, string Label, float Width, Action Invoke);
