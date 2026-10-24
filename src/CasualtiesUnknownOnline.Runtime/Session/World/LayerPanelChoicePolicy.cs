namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Who owns the end-of-layer panel's two choices. The user's 2026-10-24 ruling: **only the host** may press
/// "Continue" (next layer) or "Save and exit"; on a member both are HIDDEN rather than refused, because a
/// control a player may not use should not be there to press.
///
/// <para>
/// The rule is deliberately small and pure so it can be a case: the panel's choices are the host's when a session
/// is active and this client is a guest. Solo play and the host keep both, which is what the game already does.
/// </para>
/// </summary>
public static class LayerPanelChoicePolicy
{
	/// <summary>True on a client that must not see (or press) either end-of-layer choice.</summary>
	public static bool HidesMemberChoices(SessionRole role, bool sessionActive) =>
		sessionActive && role == SessionRole.Guest;
}
