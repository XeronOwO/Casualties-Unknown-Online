using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The carried-item fact events (host → guest): one carried item's
/// authoritative state changed — a use flipped its component state, a slot
/// move re-homed it, a pickup brought it into the inventory. The host
/// broadcasts the full fact (reliable — a lost event self-heals on the next
/// 1 Hz character snapshot); the owner itself is excluded, its local copy
/// already IS the fact. Pure wire surface, no table state — split out of
/// ItemService when the 600-line gate demanded it.
/// </summary>
public sealed class ItemCarriedSyncService(
	ISessionControl session,
	PacketSender sender,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger _log = log;

	/// <summary>The authoritative fact of one carried item changed (host broadcast: use/slot move/pickup) — the adapter updates the owner's per-player fact table and re-renders the clone. Fired on the guests from the wire and on the host directly (its own arbitration decisions).</summary>
	public event Action<ulong, CharacterItemMsg, bool>? ItemCarriedSyncReceived;

	/// <summary>
	/// Host only: broadcast one carried item's authoritative fact to every guest
	/// except its owner — a use flipped its state, a slot move re-homed it, a
	/// pickup brought it in. The peers update the owner's per-player fact table
	/// and re-render the clone immediately (reliable — a lost event self-heals
	/// on the next 1 Hz character snapshot). The owner itself is excluded: its
	/// local copy already IS the fact. SlotKnown = the carried slot is
	/// meaningful (SlotIndex != -1 — -1 is "not in any slot or limb").
	/// </summary>
	public void SendItemCarriedSync(ulong ownerSteamId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var msg = new ItemCarriedSyncMsg
		{
			OwnerSteamId = ownerSteamId,
			Item = item,
			SlotKnown = item.SlotIndex != -1,
		};
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.ItemCarriedSync, msg, reliable: true, excludeSteamId: ownerSteamId);
		_log.LogInformation("[CarriedSync] broadcast {Type} (id {ItemId}) of {Owner} slot {Slot}.", item.ItemId, item.InstanceId, ownerSteamId, item.SlotIndex);
	}

	public void FireItemCarriedSyncReceived(ulong sender, ulong ownerSteamId, CharacterItemMsg item, bool slotKnown)
		=> ItemCarriedSyncReceived?.Invoke(ownerSteamId, item, slotKnown);

	/// <summary>Host only: an arbitration adopted/recorded a carried item's new
	/// fact — apply it locally (this host's clone of the owner re-renders) and
	/// broadcast it to the peers. The owner's local copy is already the fact.</summary>
	public void Publish(ulong ownerSteamId, CharacterItemMsg item)
	{
		ItemCarriedSyncReceived?.Invoke(ownerSteamId, item, item.SlotIndex != -1);
		SendItemCarriedSync(ownerSteamId, item);
	}
}
