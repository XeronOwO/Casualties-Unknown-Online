namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// Where a remote player's render clone currently is. The Online UI pins
/// nameplates and off-screen indicators to the visible head instead of the
/// body-root/center, so the read is presentation-only: nothing here mutates the
/// clone or any authoritative state.
/// </summary>
public interface IPlayerAnchorQuery
{
	/// <summary>
	/// Gets the current world-space head position of a remote player's render
	/// clone. The Online UI uses this to pin nameplates/off-screen indicators
	/// to the visible head instead of the body-root/center. Returns false when
	/// no live render clone exists yet.
	/// </summary>
	bool TryGetRemoteHeadPosition(ulong steamId, out float x, out float y);
}
