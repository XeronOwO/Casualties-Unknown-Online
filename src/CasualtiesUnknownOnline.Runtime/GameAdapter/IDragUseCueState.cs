namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The drag gesture as this client presents it: whether an accepted item is being dragged
/// over a player, and WHICH player a release would use it on. The plugin reads it every
/// frame and draws the label; the game adapter answers it from the same nearest-player
/// resolution the release itself uses, so the label cannot name a player the release would
/// not pick.
/// <para>
/// User ruling 2026-10-06: a gesture with no UI reads as a feature that does not exist,
/// and the cue has to NAME the target because two players standing on each other cannot
/// be told apart by a highlight.
/// </para>
/// </summary>
public interface IDragUseCueState
{
	/// <summary>True while an item the family gate accepts is dragged over a player the release would use it on.</summary>
	bool Showing { get; }

	/// <summary>The player the release would use the item on; meaningful only while <see cref="Showing"/>.</summary>
	ulong TargetSteamId { get; }
}
