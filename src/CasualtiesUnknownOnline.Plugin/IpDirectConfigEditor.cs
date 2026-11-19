using System;
using BepInEx.Configuration;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Owns the BepInEx config entries for the non-Steam IP-direct path: the
/// listener port, the host address/port a guest joins, the custom in-game
/// display name used instead of a Steam persona, and the client's own
/// persistent identity. The Runtime keeps no config state; this plugin-side
/// editor is the single write surface for the Online UI and the plugin's
/// host/join actions.
///
/// The identity exists because a display name is not an identity: two players
/// may pick the same one (allowed), and until 2026-11-19 that collapsed both
/// onto one stored character. The GUID is generated once, saved here, and
/// rides the handshake; the host folds it into a character key only when two
/// present players share a name.
/// </summary>
internal sealed class IpDirectConfigEditor
{
	private readonly ConfigFile _config;
	private readonly ConfigEntry<int> _listenPort;
	private readonly ConfigEntry<string> _joinAddress;
	private readonly ConfigEntry<int> _joinPort;
	private readonly ConfigEntry<string> _displayName;
	private readonly ConfigEntry<string> _clientIdentity;

	internal IpDirectConfigEditor(
		ConfigFile config,
		ConfigEntry<int> listenPort,
		ConfigEntry<string> joinAddress,
		ConfigEntry<int> joinPort,
		ConfigEntry<string> displayName,
		ConfigEntry<string> clientIdentity)
	{
		_config = config;
		_listenPort = listenPort;
		_joinAddress = joinAddress;
		_joinPort = joinPort;
		_displayName = displayName;
		_clientIdentity = clientIdentity;
		if (string.IsNullOrWhiteSpace(_clientIdentity.Value))
		{
			_clientIdentity.Value = Guid.NewGuid().ToString("N");
			_config.Save();
		}
	}

	internal int ListenPort => _listenPort.Value;

	internal string JoinAddress => _joinAddress.Value;

	internal int JoinPort => _joinPort.Value;

	internal string DisplayName => _displayName.Value;

	/// <summary>The client's own persistent identity (a 32-character hex GUID generated on first use).</summary>
	internal string ClientIdentity => _clientIdentity.Value;

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
