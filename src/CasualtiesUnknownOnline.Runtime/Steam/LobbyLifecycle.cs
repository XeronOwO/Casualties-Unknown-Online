namespace CasualtiesUnknownOnline.Runtime.Steam;

/// <summary>
/// The client's lobby-lifecycle state (pure logic): which lobby it currently
/// hosts or has joined, and the verdict that creating a NEW lobby must first
/// leave the current one. Steam requires a leave before a fresh create — a
/// re-create without leaving strands the old lobby: the host's friends keep
/// joining the dead lobby while the host is already in the new one (observed
/// live: F8 re-create left the old lobby with a residual member and the guest
/// could not connect). SteamService owns the actual SteamMatchmaking calls;
/// this owns the current-lobby truth and the leave-before-create verdict.
/// </summary>
public sealed class LobbyLifecycle
{
	private ulong _currentLobbyId;

	/// <summary>The lobby this client currently hosts or joined (0 = none).</summary>
	public ulong CurrentLobbyId => _currentLobbyId;

	/// <summary>Whether a new lobby creation must first leave the current lobby.</summary>
	public bool MustLeaveBeforeCreate => _currentLobbyId != 0;

	/// <summary>A lobby was created or joined — it is now the current lobby.</summary>
	public void OnLobbyAcquired(ulong lobbyId) => _currentLobbyId = lobbyId;

	/// <summary>The client left its lobby — no current lobby.</summary>
	public void OnLobbyLeft() => _currentLobbyId = 0;
}
