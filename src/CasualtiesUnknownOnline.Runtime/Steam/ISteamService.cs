using System;
using CasualtiesUnknownOnline.Runtime.OnlineUi;

namespace CasualtiesUnknownOnline.Runtime.Steam;

/// <summary>
/// The Steam client API surface the runtime consumes: the local identity, the
/// lobby ownership/membership queries and the lobby lifecycle events.
/// Steamworks types never cross this boundary (abstraction rule in
/// architecture.md) — consumers only see ulong SteamIDs. The test suite's
/// FakeSteamService implements the same surface to drive the session stack
/// without a Steam client.
/// </summary>
public interface ISteamService
{
	bool IsInitialized { get; }

	/// <summary>True while the transport or lobby this service represents is joined: the Steam lobby
	/// is live, or the IP-direct transport is connected. A session that ended with this false cannot be
	/// rebuilt by a returning host, so the identity must not outlive it (the ghost-role defect: a
	/// client back at the menu with role Guest refused every join until the process restarted).</summary>
	bool IsActive { get; }

	ulong LocalSteamId { get; }

	/// <summary>SteamID of the lobby owner — the authority in a star network.</summary>
	ulong GetLobbyOwner();

	/// <summary>SteamIDs of all current lobby members (including self).</summary>
	ulong[] GetLobbyMembers();

	/// <summary>The Steam persona name for the local user or a lobby member
	/// (Online UI nameplates/status; falls back to the SteamID hex in the UI
	/// when Steam returns an empty name).</summary>
	string GetPersonaName(ulong steamId);

	/// <summary>The local player's manually selected marker color, or null when
	/// the player uses the deterministic SteamId palette assignment. This is
	/// part of the local identity surface: the handshake and roster messages
	/// carry it so every peer renders the owner's chosen presentation color.</summary>
	PlayerColorValue? LocalPlayerColor { get; }

	/// <summary>Sets the local player's selected marker color on every identity
	/// path (Steam and IP-direct), so a configuration change applies immediately
	/// and survives a transport mode switch.</summary>
	void SetLocalPlayerColor(PlayerColorValue? color);

	/// <summary>
	/// Leave the current lobby (Online UI's Leave, or the session's own dead-lobby exit). The leave is
	/// reported through <see cref="LobbyLeft"/>, so the session layer tears down with the identity cleared.
	/// A client that is not in a lobby ignores the request.
	/// </summary>
	void LeaveLobby();

	event Action<ulong>? LobbyCreated;

	event Action<ulong>? LobbyEntered;

	/// <summary>Raised when the client left the current lobby (before a new create/join request — the session layer tears down on this).</summary>
	event Action<ulong>? LobbyLeft;
}
