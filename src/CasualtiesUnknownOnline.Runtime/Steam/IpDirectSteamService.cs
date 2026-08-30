using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Steam;

/// <summary>
/// The IP-direct lobby identity adapter. It presents the TCP transport as the
/// same lobby/roster surface the session layer already expects from Steam:
/// the host is logical peer id 1, active guests are the lobby members, and
/// lobby create/enter/left events drive the existing SessionService state
/// machine unchanged. No Steam client is involved — this is purely the
/// non-Steam identity path.
/// </summary>
public sealed class IpDirectSteamService(IpDirectTransport transport, ILogger<IpDirectSteamService> log) : ISteamService
{
	private readonly Dictionary<ulong, string> _personas = [];
	private bool _isActive;
	private string _localDisplayName = "";
	private PlayerColorValue? _localPlayerColor;

	public bool IsInitialized => true;

	public ulong LocalSteamId => _isActive ? transport.LocalPeerId : 0;

	/// <summary>True while an IP-direct host/guest session is established.</summary>
	public bool IsActive => _isActive;

	/// <summary>The configured in-game display name used by IP-direct sessions.</summary>
	public string LocalDisplayName => _localDisplayName;

	public PlayerColorValue? LocalPlayerColor => _localPlayerColor;

	public void SetLocalPlayerColor(PlayerColorValue? color) => _localPlayerColor = color;

	public event Action<ulong>? LobbyCreated;

	public event Action<ulong>? LobbyEntered;

	public event Action<ulong>? LobbyLeft;

	public ulong GetLobbyOwner() => IpDirectTransport.HostPeerId;

	public ulong[] GetLobbyMembers()
	{
		if (!_isActive)
		{
			return [];
		}

		return [.. transport.ActiveRemotePeers];
	}

	public string GetPersonaName(ulong steamId)
	{
		if (steamId == LocalSteamId)
		{
			return string.IsNullOrWhiteSpace(_localDisplayName)
				? $"player-{steamId:X}"
				: _localDisplayName;
		}

		return _personas.TryGetValue(steamId, out var name) && !string.IsNullOrWhiteSpace(name)
			? name
			: $"player-{steamId:X}";
	}

	/// <summary>Sets the local custom display name (persisted by the plugin config).</summary>
	public void SetDisplayName(string name) => _localDisplayName = (name ?? "").Trim();

	/// <summary>Records a remote peer's display name for fallback lookups in IP-direct mode.</summary>
	public void SetPersonaName(ulong steamId, string name)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			_personas[steamId] = name.Trim();
		}
	}

	public bool StartHost(int port, out string error)
	{
		if (_isActive)
		{
			error = "An IP-direct session is already active.";
			return false;
		}

		if (!IpDisplayNamePolicy.TryValidate(_localDisplayName, out error))
		{
			return false;
		}

		if (!transport.StartHost(port, out error))
		{
			return false;
		}

		_isActive = true;
		log.LogInformation("IP-direct lobby identity: hosting as peer {Peer}.", transport.LocalPeerId);
		LobbyCreated?.Invoke(IpDirectTransport.HostPeerId);
		LobbyEntered?.Invoke(IpDirectTransport.HostPeerId);
		return true;
	}

	public bool Connect(string host, int port, out string error)
	{
		if (_isActive)
		{
			error = "An IP-direct session is already active.";
			return false;
		}

		if (!IpDisplayNamePolicy.TryValidate(_localDisplayName, out error))
		{
			return false;
		}

		if (!transport.Connect(host, port, out error))
		{
			return false;
		}

		_isActive = true;
		log.LogInformation("IP-direct lobby identity: joined host {Host} as peer {Peer}.", host, transport.LocalPeerId);
		LobbyEntered?.Invoke(IpDirectTransport.HostPeerId);
		return true;
	}

	public void Disconnect()
	{
		if (!_isActive)
		{
			return;
		}

		var lobbyId = IpDirectTransport.HostPeerId;
		_isActive = false;
		_personas.Clear();
		transport.Disconnect();
		log.LogInformation("IP-direct lobby identity ended.");
		LobbyLeft?.Invoke(lobbyId);
	}
}
