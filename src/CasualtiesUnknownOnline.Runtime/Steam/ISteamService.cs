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

	event Action<ulong>? LobbyCreated;

	event Action<ulong>? LobbyEntered;
}
