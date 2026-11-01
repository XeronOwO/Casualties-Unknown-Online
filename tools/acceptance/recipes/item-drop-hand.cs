// recipe: item-drop-hand
// args: slot=n
// returns: ok, slot, held, dropped
//
// Drops the item in one inventory slot through the game's own entry — `Body.DropItem(int)` (reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:1431) —
// which is exactly what the game's own drop key runs: it takes the slot's `Item`, sets `rb.simulated = true`, copies the body's velocity, unparents it and
// restores its scale. The `item-drop` recipe creates a raw object instead, and a raw object never collides, so it can only ever prove the SYNC path, not the
// player's own drop.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => { var slot = (int)({{n:slot}}); var body = PlayerCamera.main == null ? null : PlayerCamera.main.body; if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; } var held = body.HoldingItem(slot); if (!held) { return "{\"ok\":false,\"error\":\"empty-slot\",\"slot\":" + slot + "}"; } body.DropItem(slot); return "{\"ok\":true,\"slot\":" + slot + ",\"held\":true,\"dropped\":true}"; }))()