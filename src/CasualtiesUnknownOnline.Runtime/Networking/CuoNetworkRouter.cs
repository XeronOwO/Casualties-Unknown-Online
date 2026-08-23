using System;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The runtime's single network composition seam. Both the Steam path and the
/// IP-direct path are kept alive in DI; this router owns which one is active
/// and exposes the active pair through the same <see cref="ISteamService"/> and
/// <see cref="INetworkTransport"/> contracts the rest of the runtime already
/// depends on. Switching happens only from the plugin (IP host/join/leave and
/// the normal Steam lobby paths); IP-direct and Steam sessions are deliberately
/// not interconnected.
/// </summary>
public sealed class CuoNetworkRouter : INetworkTransport, ISteamService, IDisposable
{
	private readonly SteamService _steamService;
	private readonly SteamTransport _steamTransport;
	private readonly IpDirectSteamService _ipDirectSteam;
	private readonly IpDirectTransport _ipDirectTransport;
	private readonly ILogger<CuoNetworkRouter> _log;
	private bool _ipDirectActive;

	public CuoNetworkRouter(
		SteamService steamService,
		SteamTransport steamTransport,
		IpDirectSteamService ipDirectSteam,
		IpDirectTransport ipDirectTransport,
		ILogger<CuoNetworkRouter> log)
	{
		_steamService = steamService;
		_steamTransport = steamTransport;
		_ipDirectSteam = ipDirectSteam;
		_ipDirectTransport = ipDirectTransport;
		_log = log;
		WireEvents();
	}

	public bool IsIpDirectActive => _ipDirectActive;

	/// <summary>The IP-direct identity adapter (lobby events + local display name).</summary>
	public IpDirectSteamService IpDirectSteam => _ipDirectSteam;

	/// <summary>The active TCP transport itself (poll/peer listing).</summary>
	public IpDirectTransport IpDirectTransport => _ipDirectTransport;

	// ---- INetworkTransport ----

	public event Action<ulong, byte[]>? MessageReceived;

	public bool SendTo(ulong peerId, byte[] data, bool reliable) =>
		_ipDirectActive ? _ipDirectTransport.SendTo(peerId, data, reliable) : _steamTransport.SendTo(peerId, data, reliable);

	public void Poll()
	{
		// The inner transports are ICuoServices and poll themselves in the main loop.
	}

	// ---- ISteamService ----

	public bool IsInitialized => _ipDirectActive ? _ipDirectSteam.IsInitialized : _steamService.IsInitialized;

	public ulong LocalSteamId => _ipDirectActive ? _ipDirectSteam.LocalSteamId : _steamService.LocalSteamId;

	public ulong GetLobbyOwner() => _ipDirectActive ? _ipDirectSteam.GetLobbyOwner() : _steamService.GetLobbyOwner();

	public ulong[] GetLobbyMembers() => _ipDirectActive ? _ipDirectSteam.GetLobbyMembers() : _steamService.GetLobbyMembers();

	public string GetPersonaName(ulong steamId) =>
		_ipDirectActive ? _ipDirectSteam.GetPersonaName(steamId) : _steamService.GetPersonaName(steamId);

	public event Action<ulong>? LobbyCreated;

	public event Action<ulong>? LobbyEntered;

	public event Action<ulong>? LobbyLeft;

	// ---- Switching ----

	public void UseSteam()
	{
		if (_ipDirectActive)
		{
			_log.LogInformation("Network router switched to Steam.");
		}

		_ipDirectActive = false;
	}

	public void UseIpDirect()
	{
		if (!_ipDirectActive)
		{
			_log.LogInformation("Network router switched to IP direct.");
		}

		_ipDirectActive = true;
	}

	public void Dispose()
	{
		_steamService.LobbyCreated -= OnSteamLobbyCreated;
		_steamService.LobbyEntered -= OnSteamLobbyEntered;
		_steamService.LobbyLeft -= OnSteamLobbyLeft;
		_steamTransport.MessageReceived -= OnSteamMessage;
		_ipDirectSteam.LobbyCreated -= OnIpLobbyCreated;
		_ipDirectSteam.LobbyEntered -= OnIpLobbyEntered;
		_ipDirectSteam.LobbyLeft -= OnIpLobbyLeft;
		_ipDirectTransport.MessageReceived -= OnIpMessage;
	}

	private void WireEvents()
	{
		_steamService.LobbyCreated += OnSteamLobbyCreated;
		_steamService.LobbyEntered += OnSteamLobbyEntered;
		_steamService.LobbyLeft += OnSteamLobbyLeft;
		_steamTransport.MessageReceived += OnSteamMessage;
		_ipDirectSteam.LobbyCreated += OnIpLobbyCreated;
		_ipDirectSteam.LobbyEntered += OnIpLobbyEntered;
		_ipDirectSteam.LobbyLeft += OnIpLobbyLeft;
		_ipDirectTransport.MessageReceived += OnIpMessage;
	}

	private void OnSteamMessage(ulong sender, byte[] data)
	{
		if (!_ipDirectActive)
		{
			MessageReceived?.Invoke(sender, data);
		}
	}

	private void OnIpMessage(ulong sender, byte[] data)
	{
		if (_ipDirectActive)
		{
			MessageReceived?.Invoke(sender, data);
		}
	}

	private void OnSteamLobbyCreated(ulong lobbyId)
	{
		if (!_ipDirectActive)
		{
			LobbyCreated?.Invoke(lobbyId);
		}
	}

	private void OnSteamLobbyEntered(ulong lobbyId)
	{
		if (!_ipDirectActive)
		{
			LobbyEntered?.Invoke(lobbyId);
		}
	}

	private void OnSteamLobbyLeft(ulong lobbyId)
	{
		if (!_ipDirectActive)
		{
			LobbyLeft?.Invoke(lobbyId);
		}
	}

	private void OnIpLobbyCreated(ulong lobbyId) => LobbyCreated?.Invoke(lobbyId);

	private void OnIpLobbyEntered(ulong lobbyId) => LobbyEntered?.Invoke(lobbyId);

	private void OnIpLobbyLeft(ulong lobbyId) => LobbyLeft?.Invoke(lobbyId);
}
