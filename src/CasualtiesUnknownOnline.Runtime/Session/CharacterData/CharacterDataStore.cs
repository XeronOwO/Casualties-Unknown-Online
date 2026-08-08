using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Character-data domain: the session-scoped character save/restore, keyed by
/// SteamId. Guests report their snapshot (1 Hz, driven by the Game Adapter);
/// the host keeps the latest per SteamID and hands it back when the same
/// player reconnects (the save outlives the session — character-data-plan).
/// Not an ICuoService: it has no pump, it only reacts to reports and
/// handshakes. Reads role/session-active through <see cref="ISessionControl"/>
/// (resolved after the session is built) — acyclic constructor graph,
/// abstract extraction (user rule).
/// </summary>
public sealed class CharacterDataStore(ISessionControl session, PacketSender sender,
	ILogger<CharacterDataStore> log) : ICharacterDataControl
{
	private readonly ISessionControl _session = session;

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
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.CharacterData, msg);
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

	/// <summary>Host: the latest report per SteamID (clone inventory rendering on body creation).</summary>
	public CharacterDataMsg? GetSavedCharacter(ulong steamId) =>
		_savedCharacters.TryGetValue(steamId, out var data) ? data : null;

	/// <summary>Host only: broadcast the host's own snapshot — the guests render the host's clone inventory from it.</summary>
	public void BroadcastHostCharacterData(CharacterDataMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.Broadcast(NetMsg.HostCharacterData, msg);
	}

	/// <summary>
	/// A character snapshot arrived — host side: a guest's 1 Hz report (render its
	/// clone inventory); guest side: the host's reconnect restore (apply once the
	/// local body exists).
	/// </summary>
	public event Action<ulong, CharacterDataMsg>? CharacterDataReceived;

	/// <summary>Guest: the host's own 1 Hz snapshot arrived — render its clone inventory (never apply).</summary>
	public event Action<CharacterDataMsg>? HostCharacterDataReceived;

	// ---- ICharacterDataControl (the packet handlers' control surface) ----

	void ICharacterDataControl.SaveCharacterData(ulong steamId, CharacterDataMsg msg) => SaveCharacterData(steamId, msg);

	void ICharacterDataControl.SendSavedCharacter(ulong steamId) => SendSavedCharacter(steamId);

	CharacterDataMsg? ICharacterDataControl.GetSavedCharacter(ulong steamId) => GetSavedCharacter(steamId);

	void ICharacterDataControl.BroadcastHostCharacterData(CharacterDataMsg msg) => BroadcastHostCharacterData(msg);

	void ICharacterDataControl.FireCharacterDataReceived(ulong sender, CharacterDataMsg msg) =>
		CharacterDataReceived?.Invoke(sender, msg);

	void ICharacterDataControl.FireHostCharacterDataReceived(CharacterDataMsg msg) =>
		HostCharacterDataReceived?.Invoke(msg);
}
