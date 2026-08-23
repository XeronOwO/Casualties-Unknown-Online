using BepInEx.Configuration;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Owns the BepInEx config entries for the non-Steam IP-direct path: the
/// listener port, the host address/port a guest joins, and the custom
/// in-game display name used instead of a Steam persona. The Runtime keeps no
/// config state; this plugin-side editor is the single write surface for the
/// Online UI and the plugin's host/join actions.
/// </summary>
internal sealed class IpDirectConfigEditor
{
	private readonly ConfigFile _config;
	private readonly ConfigEntry<int> _listenPort;
	private readonly ConfigEntry<string> _joinAddress;
	private readonly ConfigEntry<int> _joinPort;
	private readonly ConfigEntry<string> _displayName;

	internal IpDirectConfigEditor(
		ConfigFile config,
		ConfigEntry<int> listenPort,
		ConfigEntry<string> joinAddress,
		ConfigEntry<int> joinPort,
		ConfigEntry<string> displayName)
	{
		_config = config;
		_listenPort = listenPort;
		_joinAddress = joinAddress;
		_joinPort = joinPort;
		_displayName = displayName;
	}

	internal int ListenPort => _listenPort.Value;

	internal string JoinAddress => _joinAddress.Value;

	internal int JoinPort => _joinPort.Value;

	internal string DisplayName => _displayName.Value;

	internal void SetListenPort(int value)
	{
		_listenPort.Value = value;
		_config.Save();
	}

	internal void SetJoinAddress(string value)
	{
		_joinAddress.Value = value;
		_config.Save();
	}

	internal void SetJoinPort(int value)
	{
		_joinPort.Value = value;
		_config.Save();
	}

	internal void SetDisplayName(string value)
	{
		_displayName.Value = value;
		_config.Save();
	}
}
