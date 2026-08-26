using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The direct player-interaction surface packet handlers and the Online UI
/// operate on. The landed slices are the cross-player item take (host moves one
/// carried item between its character-data snapshots and tells the two
/// participants to apply the authoritative body mutation) and the cross-player
/// carry/release (host records one carrier/one carried relation and broadcasts
/// the authoritative state so the carried player's own client follows the
/// carrier).
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

	/// <summary>Any role: request to carry another player (guest → host on the wire; host handles locally).</summary>
	void SendCarryStartRequest(ulong targetSteamId);

	/// <summary>Any role: request to climb onto a conscious-alive teammate's back (guest → host on the wire; host handles locally).</summary>
	void SendPiggybackRequest(ulong targetSteamId);

	/// <summary>Any role: request a conscious-alive teammate to ride on the local player's back (guest → host on the wire; host handles locally).</summary>
	void SendCarryOnBackRequest(ulong targetSteamId);

	/// <summary>Any role: request to release the currently carried player (guest → host on the wire; host handles locally).</summary>
	void SendCarryStopRequest(ulong carriedSteamId);

	/// <summary>Host only: a carry-start request arrived (from the wire or the host's own UI).</summary>
	void HandleCarryStartRequest(ulong sender, PlayerCarryStartRequestMsg msg);

	/// <summary>Host only: a carry-stop request arrived (from the wire or the host's own UI).</summary>
	void HandleCarryStopRequest(ulong sender, PlayerCarryStopRequestMsg msg);

	/// <summary>Raise a received carry-state broadcast for the Game Adapter and UI to apply locally (wire handler path).</summary>
	void FireCarryStateReceived(PlayerCarryStateMsg msg);

	/// <summary>An authoritative carry relation changed — the Game Adapter sets/clears the local carried-body driver; the UI refreshes buttons.</summary>
	event Action<PlayerCarryStateMsg>? CarryStateChanged;

	/// <summary>Read-only UI mirror: who currently carries the given player, if any.</summary>
	bool TryGetCarrier(ulong carriedSteamId, out ulong carrierSteamId);

	/// <summary>Read-only UI mirror: whom the given player currently carries, if any.</summary>
	bool TryGetCarried(ulong carrierSteamId, out ulong carriedSteamId);

	/// <summary>Any role: request a heal from the Online UI (guest → host on the wire; host handles locally). ItemInstanceId 0 = host auto-selects a carried medical item.</summary>
	void SendHealRequest(ulong targetSteamId, ulong itemInstanceId = 0);

	/// <summary>Host only: a heal request arrived (from the wire or the host's own UI).</summary>
	void HandleHealRequest(ulong sender, PlayerHealRequestMsg msg);

	/// <summary>Raise a received heal result for the Game Adapter to apply locally (wire handler path).</summary>
	void FireHealReceived(PlayerHealResultMsg msg);

	/// <summary>An authoritative cross-player heal result arrived — the Game Adapter consumes the healer's item and/or applies the target's post-heal state.</summary>
	event Action<PlayerHealResultMsg>? HealReceived;

	/// <summary>Any role: request a consumable use from the Online UI (guest → host on the wire; host handles locally). ItemInstanceId 0 = host auto-selects a carried drink/food.</summary>
	void SendUseRequest(ulong targetSteamId, ulong itemInstanceId = 0);

	/// <summary>Host only: a consumable-use request arrived (from the wire or the host's own UI).</summary>
	void HandleUseRequest(ulong sender, PlayerItemUseRequestMsg msg);

	/// <summary>Raise a received consumable-use result for the Game Adapter to apply locally (wire handler path).</summary>
	void FireUseReceived(PlayerItemUseResultMsg msg);

	/// <summary>An authoritative cross-player consumable-use result arrived — the Game Adapter consumes/updates the user's item and/or applies the target's post-use state.</summary>
	event Action<PlayerItemUseResultMsg>? UseReceived;

	/// <summary>Any role: request a push/shove on an in-world player (guest → host on the wire; host handles locally).</summary>
	void SendPushRequest(ulong targetSteamId);

	/// <summary>Host only: a push request arrived (from the wire or the host's own UI).</summary>
	void HandlePushRequest(ulong sender, PlayerPushRequestMsg msg);

	/// <summary>Raise a received push result for the Game Adapter to apply locally (wire handler path).</summary>
	void FirePushReceived(PlayerPushResultMsg msg);

	/// <summary>An authoritative cross-player push result arrived — the local target ragdolls/pushes and/or the local pusher pays stamina; every side plays the sound.</summary>
	event Action<PlayerPushResultMsg>? PushReceived;
}
