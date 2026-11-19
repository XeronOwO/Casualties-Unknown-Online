using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The IP-direct voice of the plugin: host/join/leave actions and the same
/// session/world guards the Steam lobby paths use. Keeping this out of the
/// BepInEx lifecycle class keeps <c>Plugin</c> a thin host and gives the
/// IP-direct actions a single testable responsibility.
/// </summary>
internal sealed class IpDirectActions
{
	private readonly CuoNetworkRouter _router;
	private readonly IpDirectSteamService _ipSteam;
	private readonly IpDirectConfigEditor _config;
	private readonly SessionService _session;
	private readonly IWorldPresenceQuery? _worldPresence;
	private readonly ILocalizationService _localization;
	private readonly ILogger<IpDirectActions> _logger;

	internal IpDirectActions(
		CuoNetworkRouter router,
		IpDirectSteamService ipSteam,
		IpDirectConfigEditor config,
		SessionService session,
		IWorldPresenceQuery? worldPresence,
		ILocalizationService localization,
		ILogger<IpDirectActions> logger)
	{
		_router = router;
		_ipSteam = ipSteam;
		_config = config;
		_session = session;
		_worldPresence = worldPresence;
		_localization = localization;
		_logger = logger;
	}

	/// <summary>The last action's user-facing error, or null when the last action succeeded or no action ran.</summary>
	internal string? LastError { get; private set; }

	internal bool CreateHost()
	{
		if (!CanCreateHost())
		{
			return false;
		}

		if (!IpDisplayNamePolicy.TryValidate(_config.DisplayName, out _))
		{
			LastError = _localization.T("ip.display_name_required");
			return false;
		}

		_ipSteam.SetDisplayName(_config.DisplayName);
		_ipSteam.SetClientIdentity(_config.ClientIdentity);
		_router.UseIpDirect();
		if (!_ipSteam.StartHost(_config.ListenPort, out var error))
		{
			LastError = error;
			_router.UseSteam();
			return false;
		}

		LastError = null;
		return true;
	}

	internal bool Join(string address, int port)
	{
		if (!CanJoinHost())
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(address))
		{
			LastError = _localization.T("ip.address_required");
			return false;
		}

		if (!IpDisplayNamePolicy.TryValidate(_config.DisplayName, out _))
		{
			LastError = _localization.T("ip.display_name_required");
			return false;
		}

		_ipSteam.SetDisplayName(_config.DisplayName);
		_ipSteam.SetClientIdentity(_config.ClientIdentity);
		_router.UseIpDirect();
		if (!_ipSteam.Connect(address.Trim(), port, out var error))
		{
			LastError = error;
			_router.UseSteam();
			return false;
		}

		LastError = null;
		return true;
	}

	internal bool Leave()
	{
		if (!_ipSteam.IsActive)
		{
			LastError = _localization.T("ip.blocked_active_session");
			return false;
		}

		_ipSteam.Disconnect();
		_router.UseSteam();
		LastError = null;
		return true;
	}

	/// <summary>
	/// Host policy: the menu is always allowed; a live world only for the true solo player, through
	/// the same <see cref="LobbySwitchGuard.CanCreateLobby"/> rule the Steam lobby create uses — the
	/// world is that player's own and the late-joiner path depends on it. Continue is CUO's own
	/// restore (`PreRunScriptLoadRunPatch` blocks the native load), so every live world a player can
	/// stand in came from a CUO run and carries the baseline a joiner generates from.
	/// </summary>
	private bool CanCreateHost()
	{
		if (!CanStart())
		{
			return false;
		}

		if (_worldPresence is not { IsInWorldOrGenerating: true })
		{
			return true;
		}

		if (LobbySwitchGuard.CanCreateLobby(_session.Role, _session.SessionActive, worldFlowActive: true))
		{
			_logger.LogInformation("IP-direct hosting opened over the solo world that is already running.");
			return true;
		}

		LastError = _localization.T("lobby.join_blocked_in_world");
		_logger.LogWarning("IP-direct start refused: a sessioned world is running or generating.");
		return false;
	}

	/// <summary>Join policy: a lobby join always changes identity, so any active world blocks it.</summary>
	private bool CanJoinHost()
	{
		if (!CanStart())
		{
			return false;
		}

		if (_worldPresence is { IsInWorldOrGenerating: true })
		{
			LastError = _localization.T("lobby.join_blocked_in_world");
			_logger.LogWarning("IP-direct join refused: a world is running or generating.");
			return false;
		}

		return true;
	}

	private bool CanStart()
	{
		if (_ipSteam.IsActive)
		{
			LastError = _localization.T("ip.blocked_active_session");
			return false;
		}

		if (_session.Role != SessionRole.None || _session.SessionActive)
		{
			LastError = _localization.T("ip.blocked_steam_session");
			_logger.LogWarning("IP-direct start refused: a Steam session is active.");
			return false;
		}

		LastError = null;
		return true;
	}
}
