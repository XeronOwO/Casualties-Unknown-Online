using System;

namespace CasualtiesUnknownOnline;

/// <summary>
/// One interaction button of a member card: its stable id, its caption, its width hint and what it does
/// (ticket online-ui-art-and-controls-overhaul, S2b).
///
/// <para>
/// It exists because the card is rendered on more than one surface — the Players page, and the quick panel
/// for its one target — and "which buttons apply to this member" must be answered once, not twice: the
/// eligibility itself comes from <see cref="OnlineUiMemberProjection"/>, and this type only carries the
/// answer to whichever surface is asking. Since S5 there is ONE renderer for it
/// (<see cref="OnlineUiMemberListDrawer.Build"/>, the display list the game's own controls draw): the IMGUI
/// twin the quick panel used to call is deleted, so the two can no longer drift apart.
/// </para>
/// </summary>
internal readonly record struct OnlineUiMemberAction(string Id, string Label, float Width, Action Invoke);
