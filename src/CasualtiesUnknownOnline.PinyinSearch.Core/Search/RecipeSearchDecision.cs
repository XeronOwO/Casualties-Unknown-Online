namespace CasualtiesUnknownOnline.PinyinSearch.Core.Search;

/// <summary>
/// The pure decision behind the crafting search scope: whether this refresh
/// extends the native filter, and with which query. It lives in Runtime, away
/// from the game-coupled patch, so the whole matrix — switch off, no query
/// typed, an item filter replacing the text filter — is testable without the
/// game (the patch layer itself is not; the test project excludes the Game
/// Adapter from compilation).
/// </summary>
internal static class RecipeSearchDecision
{
	/// <summary>
	/// The query the refresh may filter by, or null to leave the native behavior
	/// exactly as it is: the switch must be on, the player must have typed
	/// something, and no item filter may be set (the native refresh ignores the
	/// text filter in that mode, so extending it would drop rows the player
	/// asked for).
	/// </summary>
	public static string? QueryFor(bool pinyinEnabled, string? filter, bool itemFilterActive)
	{
		if (!pinyinEnabled || string.IsNullOrEmpty(filter) || itemFilterActive)
		{
			return null;
		}

		return filter;
	}
}
