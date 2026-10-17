namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// What the drag gesture should announce, if anything. The gesture is the only entry
/// point for cross-player use — the Online UI's static "Use" buttons were removed on
/// purpose — and it showed nothing at all, so a player who was never told about it saw
/// a held item and no reason to drag it onto a teammate (user finding 2026-10-06).
/// <para>
/// It carries the target's STEAM ID rather than a display name: the name the label shows
/// is the one the Online UI's member list shows, with the <c>player-{steamId}</c>
/// fallback for a peer that reports none, and that resolution already exists on the
/// presentation side (<c>OnlineUiContext.DisplayName</c>). Deciding it here would be a
/// second answer to the same question.
/// </para>
/// </summary>
internal sealed record DragUseCue(bool Showing, ulong TargetSteamId)
{
	/// <summary>Nothing to announce: no eligible item, no player under the cursor, or no session.</summary>
	internal static DragUseCue Hidden { get; } = new(false, 0);

	/// <summary>An eligible item is over the named player, so a release would apply it to them.</summary>
	internal static DragUseCue Named(ulong targetSteamId) => new(true, targetSteamId);
}
