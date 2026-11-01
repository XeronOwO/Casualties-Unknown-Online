namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Builds display labels for the in-world member context menu from the projected
/// row data. Keeping label composition pure lets UI rendering consume localized
/// text without embedding formatting in Unity GUI code.
/// </summary>
public static class OnlineUiMemberLabel
{
	/// <summary>
	/// How many characters the title may carry. The panel is a fixed 240 px wide and the title is drawn on one line, so a
	/// long display name used to push the dead suffix past the edge and the marker disappeared — the one piece of the title
	/// that must never be lost. The budget is deliberately coarse: the suffix is short and always survives, and a name that
	/// fits is never touched.
	/// </summary>
	public const int MaxTitleChars = 28;

	private const string Ellipsis = "…";

	/// <summary>
	/// Formats a member's context-menu title.
	/// </summary>
	/// <param name="displayName">The member's display name.</param>
	/// <param name="isDead">Whether the member's projected vitals show a dead body.</param>
	/// <param name="deadSuffix">The localized dead-state suffix to append when <paramref name="isDead"/> is true.</param>
	public static string FormatContextTitle(string displayName, bool isDead, string deadSuffix)
	{
		if (!isDead)
		{
			return displayName;
		}

		var budget = MaxTitleChars - deadSuffix.Length;
		if (budget < 1)
		{
			return deadSuffix;
		}

		if (displayName.Length <= budget)
		{
			return displayName + deadSuffix;
		}

		// Keep the marker, shorten the NAME: the suffix carries the state the player acts on, the name is what they already
		// know from the rest of the panel.
		return displayName.Substring(0, budget - Ellipsis.Length) + Ellipsis + deadSuffix;
	}
}
