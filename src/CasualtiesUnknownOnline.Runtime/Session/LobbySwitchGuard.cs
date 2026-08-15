namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Lobby-switch policy (PURE — no Unity, no Steam, no session state): a lobby
/// identity may change freely from the main menu, but not while a world is
/// running or generating. The one exception is the existing solo-in-world ->
/// F8-create-lobby flow: there is no session yet, the generated world is the
/// solo player's own and the late-joiner snapshot path depends on it.
/// </summary>
public static class LobbySwitchGuard
{
	/// <summary>
	/// Whether creating a lobby is allowed. A world/generation blocks every
	/// sessioned role; a true solo player (Role=None, no active session) may
	/// still turn its running world into a host lobby.
	/// </summary>
	public static bool CanCreateLobby(SessionRole role, bool sessionActive, bool worldFlowActive)
	{
		if (!worldFlowActive)
		{
			return true;
		}

		return role == SessionRole.None && !sessionActive;
	}

	/// <summary>Whether joining another lobby is allowed. Joining always changes identity, so any world/generation blocks it.</summary>
	public static bool CanJoinLobby(bool worldFlowActive) => !worldFlowActive;
}
