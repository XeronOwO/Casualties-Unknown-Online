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
	private readonly IGameAdapter? _adapter;
	private readonly ILocalizationService _localization;
	private readonly ILogger<IpDirectActions> _logger;

	internal IpDirectActions(
		CuoNetworkRouter router,
		IpDirectSteamService ipSteam,
		IpDirectConfigEditor config,
		SessionService session,
		IGameAdapter? adapter,
		ILocalizationService localization,
		ILogger<IpDirectActions> logger)
	{
		_router = router;
		_ipSteam = ipSteam;
		_config = config;
		_session = session;
		_adapter = adapter;
		_localization = localization;
		_logger = logger;
	}

	/// <summary>The last action's user-facing error, or null when the last action succeeded or no action ran.</summary>
	internal string? LastError { get; private set; }

	internal bool CreateHost()
	{
		if (!CanStart())
		{
			return false;
		}

		if (!IpDisplayNamePolicy.TryValidate(_config.DisplayName, out _))
		{
			LastError = _localization.T("ip.display_name_required");
			return false;
		}

		_ipSteam.SetDisplayName(_config.DisplayName);
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
		if (!CanStart())
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

		if (_adapter is { IsInWorldOrGenerating: true })
		{
			LastError = _localization.T("lobby.join_blocked_in_world");
			_logger.LogWarning("IP-direct start refused: a world is running or generating.");
			return false;
		}

		LastError = null;
		return true;
	}
}
