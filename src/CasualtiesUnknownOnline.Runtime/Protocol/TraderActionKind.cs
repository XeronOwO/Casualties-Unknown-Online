namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// Player → trader interaction kinds (the trader's UI buttons, PlayerCamera.cs:
/// 2584-2638). Values start at 1 — protobuf omits zero, and Kind is never
/// "unset". The trader's state is host-authoritative: the acting side runs the
/// full local method (immediate player-side feedback) and reports; the host
/// executes the trader-side state change (TradeExecutor) and broadcasts the
/// authoritative state; every side applies the broadcast (a full overwrite,
/// which also rolls back a rejected concurrent purchase).
/// </summary>
public enum TraderActionKind : byte
{
	MeetPlayer = 1, // the player walked within 6 units of a trader that has not started a conversation (OnWillRenderObject → MeetPlayer, TraderScript.cs:276-287) — the host rolls the reputation base
	Purchase = 2, // TryPurchase (TraderScript.cs:747-804) — the acting side creates the item (its only spawn), the host removes it from the stock
	GiveItem = 3, // GiveItem (TraderScript.cs:604-639) — the acting side destroys the item (the item domain reports), the host credits the value
	Haggle = 4, // TryHaggle (TraderScript.cs:220-265) — the host rolls the reputation change
	Threaten = 5, // Threaten (TraderScript.cs:517-545) — the host rolls the outcome
	Hug = 6, // TryHug (TraderScript.cs:448-481) — the host decides success/failure
	MoveTo = 7, // AskToMove (TraderScript.cs:89-104) — the host decides the acceptance (reputation gate) and the destination is deterministic
}
