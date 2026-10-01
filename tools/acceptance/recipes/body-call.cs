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
	if (kind == "vomit") {
		var vomiter = body.GetComponent<Vomiter>();
		if (vomiter == null) { return "{\"ok\":false,\"error\":\"no-vomiter\"}"; }
		vomiter.Vomit();
		return "{\"ok\":true,\"kind\":\"vomit\",\"applied\":true}";
	}
	return "{\"ok\":false,\"error\":\"bad-kind\",\"detail\":\"kind must be switch, nap or vomit\"}";
}))()
