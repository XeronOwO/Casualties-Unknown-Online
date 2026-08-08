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
/// handshakes. Reads role/session-active from <see cref="SessionIdentity"/> and
/// <see cref="SessionState"/> instead of depending on SessionService itself —
/// acyclic constructor graph, abstract extraction (user rule).
/// </summary>
public sealed class CharacterDataStore(SessionIdentity identity, SessionState state, PacketSender sender,
	ILogger<CharacterDataStore> log) : ICharacterDataControl
{
	private readonly SessionIdentity _identity = identity;
	private readonly SessionState _state = state;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<CharacterDataStore> _log = log;
	private readonly Dictionary<ulong, CharacterDataMsg> _savedCharacters = []; // host: last report per SteamID

	/// <summary>
	/// Guest side: report the local character snapshot to the host (1-2 Hz,
	/// driven by the Game Adapter). The host keeps the latest per SteamID and
	/// hands it back when the same player reconnects.
	/// </summary>
	public void ReportCharacterData(CharacterDataMsg msg)
	{
		if (_identity.Role != SessionRole.Guest || !_state.SessionActive)
		{
			return;
		}

		_sender.Send(_identity.HostSteamId, NetMsg.CharacterData, msg);
	}

	/// <summary>Host side: keep the latest report per SteamID (session-scoped save).</summary>
	internal void SaveCharacterData(ulong steamId, CharacterDataMsg msg) => _savedCharacters[steamId] = msg;

	/// <summary>Host side: hand the saved character data back to a reconnecting player.</summary>
	internal void SendSavedCharacter(ulong steamId)
	{
		if (_savedCharacters.TryGetValue(steamId, out var data))
		{
			_sender.Send(steamId, NetMsg.CharacterData, data);
			_log.LogInformation("Sent saved character data to {Peer} ({Items} items).", steamId, data.Items.Count);
		}
	}

	/// <summary>
	/// Guest side: the host sent a saved character snapshot back (reconnect
	/// restore) — apply it in the Game Adapter once the local body exists.
	/// </summary>
	public event Action<CharacterDataMsg>? CharacterDataReceived;

	// ---- ICharacterDataControl (the packet handlers' control surface) ----

	void ICharacterDataControl.SaveCharacterData(ulong steamId, CharacterDataMsg msg) => SaveCharacterData(steamId, msg);

	void ICharacterDataControl.SendSavedCharacter(ulong steamId) => SendSavedCharacter(steamId);

	void ICharacterDataControl.FireCharacterDataReceived(CharacterDataMsg msg) => CharacterDataReceived?.Invoke(msg);
}
