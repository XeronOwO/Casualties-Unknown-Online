using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The direct player-interaction surface packet handlers and the Online UI
/// operate on. The landed slices are the cross-player item take (host moves one
/// carried item between its character-data snapshots and tells the two
/// participants to apply the authoritative body mutation) and the cross-player
/// carry/release (host commits one carrier/one carried kernel carry fact; the
/// committed batch projection updates the carry mirrors so the carried player's
/// own client follows the carrier).
/// </summary>
public interface IPlayerInteractionControl
{
	/// <summary>The generic remote medical operation session surface (incremental injection in Stage 1; shared minigame sessions in later stages).</summary>
	IMedicalOperationControl MedicalOperations { get; }

	/// <summary>Any role: request a take from the Online UI (guest → host on the wire; host handles locally).</summary>
	void SendTakeRequest(ulong ownerSteamId, ulong itemInstanceId);

	/// <summary>Host only: a take request arrived (from the wire or the host's own UI).</summary>
	void HandleTakeRequest(ulong sender, PlayerInventoryTakeRequestMsg msg);

	/// <summary>Any role: report one native inventory intent captured from the local drag release — guest → host on the wire, host handles locally.</summary>
	void SendRemoteInventoryIntent(RemoteInventoryIntentMsg msg);

	/// <summary>Host only: a native inventory intent arrived (from the wire or the host's own client).</summary>
	void HandleRemoteInventoryIntentRequest(ulong sender, RemoteInventoryIntentMsg msg);

	/// <summary>Any role: a host-validated native inventory intent must be replayed on the local player's own body.</summary>
	void FireRemoteInventoryIntentReceived(RemoteInventoryIntentMsg msg);

	/// <summary>An authoritative host-validated native inventory intent arrived for the local player's own body.</summary>
	event Action<RemoteInventoryIntentMsg>? RemoteInventoryIntentReceived;

	/// <summary>
	/// Receiving client: the host asks where this give would land on THIS client's body, so it can answer with
	/// its own rules before any custody moves (the user's 2026-10-22 ruling). The answer names the slot or the
	/// container this client will use, or the reason it refuses.
	/// </summary>
	void HandlePlacementQuery(ulong sender, PlayerPlacementQueryMsg msg);

	/// <summary>Host only: the receiving client answered a placement question — commit the answered destination or refuse the giver.</summary>
	void HandlePlacementAnswer(ulong sender, PlayerPlacementAnswerMsg msg);

	/// <summary>Raise a received transfer for the Game Adapter to apply locally (kernel projection path).</summary>
	void FireTransferReceived(PlayerInventoryTransferMsg msg);

	/// <summary>An authoritative cross-player inventory transfer arrived — the Game Adapter applies the body mutation.</summary>
	event Action<PlayerInventoryTransferMsg>? TransferReceived;

	/// <summary>
	/// Any role: the console <c>/give</c> verb — create ONE item in a player's inventory
	/// with its state already set. The host creates and delivers and returns the answer
	/// sentence; a member sends the request and returns null, because its answer arrives
	/// through <see cref="GiveAnswered"/>. The caller has already applied the cheat gate and
	/// resolved the ONE target body; the state is the canonical SNBT text attached to the
	/// item id (empty = the definition's own capture), parsed and bound where the item is
	/// created.
	/// </summary>
	string? GiveItem(ulong targetSteamId, string definitionId, int slot, string stateText);

	/// <summary>Host only: a member's give request arrived — the host judges its own cheat switch and roster, creates the item, and answers.</summary>
	void HandleGiveRequest(ulong sender, PlayerInventoryGiveRequestMsg msg);

	/// <summary>Member: the host's answer to a give request arrived.</summary>
	void FireGiveAnswered(PlayerInventoryGiveResultMsg msg);

	/// <summary>The host answered a give request — the console prints the sentence where the verb was typed.</summary>
	event Action<PlayerInventoryGiveResultMsg>? GiveAnswered;

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

	/// <summary>An authoritative carry relation changed — the Game Adapter sets/clears the local carried-body driver; the UI refreshes buttons.</summary>
	event Action<PlayerCarryStateMsg>? CarryStateChanged;

	/// <summary>Read-only UI mirror: who currently carries the given player, if any.</summary>
	bool TryGetCarrier(ulong carriedSteamId, out ulong carrierSteamId);

	/// <summary>Read-only UI mirror: whom the given player currently carries, if any.</summary>
	bool TryGetCarried(ulong carrierSteamId, out ulong carriedSteamId);

	/// <summary>Any role: request a heal from the Online UI (guest → host on the wire; host handles locally). ItemInstanceId 0 = host auto-selects a carried medical item; targetLimbIndex -1 = most-injured auto pick.</summary>
	void SendHealRequest(ulong targetSteamId, ulong itemInstanceId = 0, int targetLimbIndex = -1);

	/// <summary>Host only: a heal request arrived (from the wire or the host's own UI).</summary>
	void HandleHealRequest(ulong sender, PlayerHealRequestMsg msg);

	/// <summary>Raise a received heal result for the Game Adapter to apply locally (kernel projection path).</summary>
	void FireHealReceived(PlayerHealResultMsg msg);

	/// <summary>An authoritative cross-player heal result arrived — the Game Adapter consumes the healer's item and/or applies the target's post-heal state.</summary>
	event Action<PlayerHealResultMsg>? HealReceived;

	/// <summary>
	/// Any role: request a consumable use from the Online UI (guest → host on the
	/// wire; host handles locally). ItemInstanceId 0 = host auto-selects a carried
	/// consumable. TargetLimbIndex is the limb the WOUND VIEW's release named; -1
	/// means the request came through the inventory use (the world drag, this
	/// one-shot use), which carries no limb. Injectable/IV medicine is not accepted
	/// through this one-shot path. DoseMl is the ml the acting client measured from
	/// the item's own native action — a topical limb application or a drink; 0 for
	/// every family the host computes for itself and for a family this path refuses.
	/// </summary>
	void SendUseRequest(ulong targetSteamId, ulong itemInstanceId = 0, int targetLimbIndex = -1, float doseMl = 0f);

	/// <summary>Host only: a consumable-use request arrived (from the wire or the host's own UI).</summary>
	void HandleUseRequest(ulong sender, PlayerItemUseRequestMsg msg);

	/// <summary>Raise a received consumable-use result for the Game Adapter to apply locally (kernel projection path).</summary>
	void FireUseReceived(PlayerItemUseResultMsg msg);

	/// <summary>An authoritative cross-player consumable-use result arrived — the Game Adapter consumes/updates the user's item and/or applies the target's post-use state.</summary>
	event Action<PlayerItemUseResultMsg>? UseReceived;

	/// <summary>
	/// Any role: the local client ran a cross-player use whose effect belongs to this
	/// side — report what the used item became so the host can hand it to the item's
	/// owner (guest → host on the wire; the host handles its own case locally,
	/// because it is the affected side there just as a guest is). The two families
	/// that run on the affected side are the solid-food eat and the limb-tool use, so
	/// the item's post-use state exists only here; the host matches the report against
	/// the use it admitted. <paramref name="consumed"/> is the limb-tool family's own
	/// observation that the run destroyed the item object itself; the eat passes false
	/// and the host keeps its own verdict. <paramref name="becameLimbComponent"/> is
	/// the same family's narrower observation that the object did not merely end — it
	/// became the limb's installed component, so the item keeps its id and gains a
	/// location on that limb.
	/// </summary>
	void SendItemActionOutcome(ulong itemInstanceId, float condition, bool consumed, bool becameLimbComponent = false);

	/// <summary>Host only: an affected-side use's outcome arrived (from the wire or the host's own client).</summary>
	void HandleItemActionOutcome(ulong sender, PlayerItemActionOutcomeMsg msg);

	/// <summary>Any role: request a push/shove on an in-world player (guest → host on the wire; host handles locally).</summary>
	void SendPushRequest(ulong targetSteamId);

	/// <summary>Host only: a push request arrived (from the wire or the host's own UI).</summary>
	void HandlePushRequest(ulong sender, PlayerPushRequestMsg msg);

	/// <summary>Raise a received push result for the Game Adapter to apply locally (wire handler path).</summary>
	void FirePushReceived(PlayerPushResultMsg msg);

	/// <summary>An authoritative cross-player push result arrived — the local target ragdolls/pushes and/or the local pusher pays stamina; every side plays the sound.</summary>
	event Action<PlayerPushResultMsg>? PushReceived;
}
