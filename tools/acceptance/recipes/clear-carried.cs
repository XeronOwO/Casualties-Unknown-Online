// recipe: clear-carried
// args: slots=n (how many inventory slots the body has; the game's own body carries six)
// returns: ok, dropped, slots
//
// Empties the local body's inventory through the game's own `Body.DropItem(int)` (reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:1431),
// which is the entry the player's drop key runs. The rows that stage an item into the member's bag need room for it, and a
// run keeps what the rows before it left behind (measured 2026-11-22: give-item, give-container-sound, crafting-outcome,
// drop-mid-transfer and two-member-continue all read "no-free-slot" late in the batch, while each passed alone).
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => { var slots = (int)({{n:slots}}); var body = PlayerCamera.main == null ? null : PlayerCamera.main.body; if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; } var dropped = 0; for (var slot = 0; slot < slots; slot++) { if (body.HoldingItem(slot)) { body.DropItem(slot); dropped++; } } return "{\"ok\":true,\"dropped\":" + dropped + ",\"slots\":" + slots + "}"; }))()
