namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The session's character-picker policy. The game's solo unlock store gates
/// the two later characters; a co-op session is not the single-player
/// progression, so the picker shows every character and the session win does
/// not write the solo unlock. Kept as a pure rule so both the picker view and
/// the win path consume one decision.
/// </summary>
public static class CharacterUnlockPolicy
{
	/// <summary>Whether the picker may offer this character: a session offers all three; solo keeps the native unlock.</summary>
	public static bool CanPick(bool inSession, bool soloUnlocked) => inSession || soloUnlocked;

	/// <summary>Whether a win may write the solo character unlock: only outside a session.</summary>
	public static bool ShouldWriteWin(bool inSession) => !inSession;
}
