namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Whether the drag gesture announces a target, decided from plain facts so the rule is
/// a case rather than a frame of behaviour nobody can replay.
/// <para>
/// The user's requirement is that the cue NAMES the player, and that is not decoration:
/// two players standing on each other cannot be told apart by a highlight, so a named
/// label is what makes a release a decision instead of a guess (user ruling 2026-10-06).
/// The target here is the same last-entered range the release itself uses for the USE, and
/// the caller hides the cue when the pointer is on the displayed backpack (where the release
/// is a GIVE), so the label and the release cannot disagree about what the release does.
/// </para>
/// <para>
/// The cue appears only for an item the family gate ACCEPTS — the ticket's own
/// recommendation — so the cue itself teaches what can be handed over, and an item that
/// cannot be shows nothing positive rather than promising a target it will not use.
/// </para>
/// <para>
/// The gesture's own release condition is one of the facts: the game's <c>iteminteract</c>
/// bind must still be held. A CUO console or window suspends the game's input, so a release
/// made under one is never seen by the game's own release branch; without this fact the label
/// would keep naming a player after the button is up.
/// </para>
/// </summary>
internal static class DragUseCuePolicy
{
	internal static DragUseCue Decide(bool sessionActive, bool hasDraggedItem, bool interactHeld, bool itemIsEligible, ulong? targetSteamId)
	{
		if (!sessionActive || !hasDraggedItem || !interactHeld || !itemIsEligible || targetSteamId is not { } target || target == 0)
		{
			return DragUseCue.Hidden;
		}

		return DragUseCue.Named(target);
	}
}
