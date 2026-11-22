// recipe: empty-hands
// args: none
// returns: ok, dropped
//
// Empties what the local body holds through the game's own `Body.DropItem(int)` (reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:1431),
// the entry the player's drop key runs. A row that swings at a block must swing with an empty hand: a carried item sits on
// the ray the game casts from the attacking limb (`Body.cs:2168`), so the first thing the swing meets is the lantern, not
// the wall (measured 2026-11-22: row 1's swing read `hitName: "lantern(Clone)"` and the cell never took damage).
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => { var body = PlayerCamera.main == null ? null : PlayerCamera.main.body; if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; } var dropped = 0; for (var slot = 0; slot < body.slots.Length; slot++) { if (body.HoldingItem(slot)) { body.DropItem(slot); dropped++; } } return "{\"ok\":true,\"dropped\":" + dropped + "}"; }))()
