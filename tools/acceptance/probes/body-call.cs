// recipe: body-call
// args: kind=s
// serves: unhooked-item-and-body-sound-families
// returns: ok, kind, applied, note, error, detail
//
// Calls one of the game's own body one-shot entry points, so the character-sound capture sees the
// native call it wraps:
//   switch — Body.SwitchHands (the inventory-gesture scope's own call)
//   nap    — Body.TakeANap (the ordinary nap routine the nap clip belongs to; it starts only when the
//            body's own canTakeNap gate holds)
//   vomit  — Vomiter.Vomit (the per-step coroutine scope that plays vomit1 / vomit2)
//   bark   — PantSound.Bark (the B-key bark the Pain/Bark kinds were added for)
//   attack — Body.UseItemInHand with an empty hand (the game's own swing entry)
((System.Func<string>)(() => {
	var kind = {{s:kind}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	if (kind == "switch") {
		body.SwitchHands();
		return "{\"ok\":true,\"kind\":\"switch\",\"applied\":true}";
	}
	if (kind == "nap") {
		body.TakeANap();
		return "{\"ok\":true,\"kind\":\"nap\",\"applied\":true,\"note\":\"starts only when canTakeNap holds\"}";
	}
	if (kind == "attack") {
		// The game's own empty-hand attack entry: `Body.UseItemInHand` falls through to `Attack` when nothing usable is held, which is exactly what a player's
		// left-click does. It exists so a swing can be produced without a fabricated `AttackInfo` (which `entity-attack` refuses to do, and for good reason).
		body.UseItemInHand();
		return "{\"ok\":true,\"kind\":\"attack\",\"applied\":true,\"holdingItem\":" + (body.HoldingItem(body.handSlot) ? "true" : "false") + "}";
	}
	if (kind == "bark") {
		var pant = body.GetComponent<PantSound>();
		if (pant == null) { return "{\"ok\":false,\"error\":\"no-pant-sound\"}"; }
		pant.Bark();
		return "{\"ok\":true,\"kind\":\"bark\",\"applied\":true}";
	}
	if (kind == "vomit") {
		var vomiter = body.GetComponent<Vomiter>();
		if (vomiter == null) { return "{\"ok\":false,\"error\":\"no-vomiter\"}"; }
		vomiter.Vomit();
		return "{\"ok\":true,\"kind\":\"vomit\",\"applied\":true}";
	}
	return "{\"ok\":false,\"error\":\"bad-kind\",\"detail\":\"kind must be switch, nap or vomit\"}";
}))()
