using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Character-data domain: the session-scoped character save/restore, keyed by
/// SteamId. Guests report their snapshot (1 Hz, driven by the Game Adapter);
/// the host keeps the latest per SteamID and hands it back when the same
/// player reconnects (the save outlives the session — character-data-plan).
/// Not an ICuoService: it has no pump, it only reacts to reports and
/// handshakes. Depends on SessionService (role/session-active gates) and
/// PacketGateway (send) — acyclic, plain constructor injection.
/// </summary>
public sealed class CharacterDataStore(SessionService session, PacketGateway gateway, ILogger<CharacterDataStore> log)
{
	private readonly SessionService _session = session;
	private readonly PacketGateway _gateway = gateway;
	private readonly ILogger<CharacterDataStore> _log = log;
	private readonly Dictionary<ulong, CharacterDataMsg> _savedCharacters = []; // host: last report per SteamID

	/// <summary>
	/// Guest side: report the local character snapshot to the host (1-2 Hz,
	/// driven by the Game Adapter). The host keeps the latest per SteamID and
	/// hands it back when the same player reconnects.
	/// </summary>
	public void ReportCharacterData(CharacterDataMsg msg)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_gateway.Send(_session.HostSteamId, NetMsg.CharacterData, msg);
	}

	/// <summary>Host side: keep the latest report per SteamID (session-scoped save).</summary>
	internal void SaveCharacterData(ulong steamId, CharacterDataMsg msg) => _savedCharacters[steamId] = msg;

	/// <summary>Host side: hand the saved character data back to a reconnecting player.</summary>
	internal void SendSavedCharacter(ulong steamId)
	{
		if (_savedCharacters.TryGetValue(steamId, out var data))
		{
			_gateway.Send(steamId, NetMsg.CharacterData, data);
			_log.LogInformation("Sent saved character data to {Peer} ({Items} items).", steamId, data.Items.Count);
		}
	}

	/// <summary>
	/// Guest side: the host sent a saved character snapshot back (reconnect
	/// restore) — apply it in the Game Adapter once the local body exists.
	/// </summary>
	public event Action<CharacterDataMsg>? CharacterDataReceived;

	internal void FireCharacterDataReceived(CharacterDataMsg msg) => CharacterDataReceived?.Invoke(msg);
}
