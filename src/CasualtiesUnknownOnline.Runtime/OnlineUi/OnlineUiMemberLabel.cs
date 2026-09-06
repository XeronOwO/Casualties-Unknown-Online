namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Builds display labels for the in-world member context menu from the projected
/// row data. Keeping label composition pure lets UI rendering consume localized
/// text without embedding formatting in Unity GUI code.
/// </summary>
public static class OnlineUiMemberLabel
{
	/// <summary>
	/// Formats a member's context-menu title.
	/// </summary>
	/// <param name="displayName">The member's display name.</param>
	/// <param name="isDead">Whether the member's projected vitals show a dead body.</param>
	/// <param name="deadSuffix">The localized dead-state suffix to append when <paramref name="isDead"/> is true.</param>
	public static string FormatContextTitle(string displayName, bool isDead, string deadSuffix)
		=> isDead ? displayName + deadSuffix : displayName;
}
