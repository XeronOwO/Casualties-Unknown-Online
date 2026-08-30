using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Steam;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// Test-controllable Steam surface: the lobby events fire on demand, the
/// membership table is set by the test — the session stack never touches a
/// real Steam client. Implements ICuoService so it can take the real
/// SteamService's lifecycle slot (the real one must never initialize in
/// tests — it would touch Steamworks).
/// </summary>
internal sealed class FakeSteamService(ulong localSteamId) : ISteamService, ICuoService
{
	public bool IsInitialized { get; } = true;

	public ulong LocalSteamId { get; internal set; } = localSteamId;

	internal ulong LobbyOwner { get; set; }

	internal ulong[] LobbyMembers { get; set; } = [];

	internal Dictionary<ulong, string> Personas { get; } = [];

	public event Action<ulong>? LobbyCreated;

	public event Action<ulong>? LobbyEntered;

	public event Action<ulong>? LobbyLeft;

	public ulong GetLobbyOwner() => LobbyOwner;

	public ulong[] GetLobbyMembers() => LobbyMembers;

	public string GetPersonaName(ulong steamId) =>
		Personas.TryGetValue(steamId, out var name) ? name : $"player-{steamId}";

	public PlayerColorValue? LocalPlayerColor { get; internal set; }

	public void SetLocalPlayerColor(PlayerColorValue? color) => LocalPlayerColor = color;

	internal void FireLobbyCreated(ulong lobbyId) => LobbyCreated?.Invoke(lobbyId);

	internal void FireLobbyEntered(ulong lobbyId) => LobbyEntered?.Invoke(lobbyId);

	internal void FireLobbyLeft(ulong lobbyId) => LobbyLeft?.Invoke(lobbyId);

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}
}
