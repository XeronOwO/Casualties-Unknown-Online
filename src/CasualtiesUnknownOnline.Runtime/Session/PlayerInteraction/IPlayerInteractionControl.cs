using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The direct player-interaction surface packet handlers and the Online UI
/// operate on. The first slice is the cross-player item take: a player requests
/// one carried item out of another in-world player's inventory; the host is the
/// authority (it owns the per-player character-data snapshots and the guest
/// transfer table), it moves the ownership record and sends the two
/// participants an authoritative body mutation.
/// </summary>
public interface IPlayerInteractionControl
{
	/// <summary>Any role: request a take from the Online UI (guest → host on the wire; host handles locally).</summary>
	void SendTakeRequest(ulong ownerSteamId, ulong itemInstanceId);

	/// <summary>Host only: a take request arrived (from the wire or the host's own UI).</summary>
	void HandleTakeRequest(ulong sender, PlayerInventoryTakeRequestMsg msg);

	/// <summary>Raise a received transfer for the Game Adapter to apply locally (wire handler path).</summary>
	void FireTransferReceived(PlayerInventoryTransferMsg msg);

	/// <summary>An authoritative cross-player inventory transfer arrived — the Game Adapter applies the body mutation.</summary>
	event Action<PlayerInventoryTransferMsg>? TransferReceived;
}
