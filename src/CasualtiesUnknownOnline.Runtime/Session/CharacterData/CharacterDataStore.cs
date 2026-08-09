using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Character-data domain: the session-scoped character save/restore, keyed by
/// SteamId. Guests report their snapshot (1 Hz, driven by the Game Adapter);
/// the host keeps the latest per SteamID and hands it back when the same
/// player reconnects (the save outlives the session — character-data-plan).
/// The reconnect restore merges the item arbitration's transfer table (the
/// host's authoritative record of what the guest owns) over the guest's last
/// report — the host's data wins where they disagree, and items the guest
/// never reported yet (a pickup moments before the disconnect) still restore.
/// Not an ICuoService: it has no pump, it only reacts to reports and
/// handshakes. Reads role/session-active through <see cref="ISessionControl"/>
/// (resolved after the session is built) and the transfer table through
/// <see cref="IItemControl"/> — acyclic constructor graph (ItemService never
/// depends on this store), abstract extraction (user rule).
/// </summary>
public sealed class CharacterDataStore(ISessionControl session, PacketSender sender,
	ILogger<CharacterDataStore> log, IItemControl items) : ICharacterDataControl
{
	private readonly ISessionControl _session = session;

	private readonly PacketSender _sender = sender;
	private readonly ILogger<CharacterDataStore> _log = log;
	private readonly IItemControl _items = items;
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

	/// <summary>Host side: hand the saved character data back to a reconnecting player, with the item arbitration's transfer table merged over it.</summary>
	internal void SendSavedCharacter(ulong steamId)
	{
		if (_savedCharacters.TryGetValue(steamId, out var data))
		{
			MergeTransferredItems(steamId, data);
			_sender.Send(steamId, NetMsg.CharacterData, data);
			_log.LogInformation("Sent saved character data to {Peer} ({Items} items).", steamId, data.Items.Count);
		}
	}

	/// <summary>
	/// Merge the host's authoritative ownership record (the transfer table —
	/// what the arbitration moved into the guest's hands, never overwritten by
	/// the guest's own reports) over the guest's last snapshot. An entry the
	/// snapshot already has is replaced by the authoritative state (the
	/// snapshot's slot is kept — a carried item's slot is its owner's local
	/// fact); an entry the snapshot lacks (a pickup moments before the
	/// disconnect) is appended.
	/// </summary>
	private void MergeTransferredItems(ulong steamId, CharacterDataMsg data)
	{
		var transferred = _items.GetTransferredItems(steamId);
		if (transferred.Count == 0)
		{
			return;
		}

		// Snapshot index: by instance id where present, else by definition id.
		var byId = new Dictionary<ulong, int>();
		var byDef = new Dictionary<string, int>();
		for (var i = 0; i < data.Items.Count; i++)
		{
			var item = data.Items[i];
			if (item.InstanceId != 0)
			{
				byId[item.InstanceId] = i;
			}
			else
			{
				byDef[item.ItemId] = i;
			}
		}

		var merged = 0;
		foreach (var entry in transferred)
		{
			var authoritative = entry.Item;
			var idx = authoritative.InstanceId != 0 && byId.TryGetValue(authoritative.InstanceId, out var byKey)
				? byKey
				: byDef.TryGetValue(authoritative.ItemId, out var byDefinition) ? byDefinition : -1;
			if (idx >= 0)
			{
				authoritative.SlotIndex = data.Items[idx].SlotIndex; // the snapshot's slot is the owner's local fact
				data.Items[idx] = authoritative;
			}
			else
			{
				data.Items.Add(authoritative);
			}

			merged++;
		}

		_log.LogInformation("Merged {Merged} transfer-table items into the restore of {Peer} ({Total} items total).",
			merged, steamId, data.Items.Count);
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

	void ICharacterDataControl.RelayCharacterData(ulong ownerSteamId, CharacterDataMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		// Stamping the shared instance is safe: it is the SAVED copy (the
		// handler saved the same reference), and the only later senders of it
		// are the owner's own restore — the receiver's owner check restores
		// exactly for OwnerSteamId == itself.
		msg.OwnerSteamId = ownerSteamId;
		_session.BroadcastExcept(ownerSteamId, NetMsg.CharacterData, msg);
		_log.LogInformation("Relayed character data of {Owner} to the other guests ({Items} items).", ownerSteamId, msg.Items.Count);
	}

	void ICharacterDataControl.FireCharacterDataReceived(ulong sender, CharacterDataMsg msg) =>
		CharacterDataReceived?.Invoke(sender, msg);

	void ICharacterDataControl.FireHostCharacterDataReceived(CharacterDataMsg msg) =>
		HostCharacterDataReceived?.Invoke(msg);
}
