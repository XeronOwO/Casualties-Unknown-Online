using System;

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

	ulong LocalSteamId { get; }

	/// <summary>SteamID of the lobby owner — the authority in a star network.</summary>
	ulong GetLobbyOwner();

	/// <summary>SteamIDs of all current lobby members (including self).</summary>
	ulong[] GetLobbyMembers();

	/// <summary>The Steam persona name for the local user or a lobby member
	/// (Online UI nameplates/status; falls back to the SteamID hex in the UI
	/// when Steam returns an empty name).</summary>
	string GetPersonaName(ulong steamId);

	event Action<ulong>? LobbyCreated;

	event Action<ulong>? LobbyEntered;

	/// <summary>Raised when the client left the current lobby (before a new create/join request — the session layer tears down on this).</summary>
	event Action<ulong>? LobbyLeft;
}
