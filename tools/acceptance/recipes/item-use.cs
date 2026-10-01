// recipe: item-use
// args: type=s aim-x=n aim-y=n
// serves: host-eating-sound-not-heard-on-guest, host-metal-scrap-block-place-sound-not-synced-to-guest,
//         unhooked-item-and-body-sound-families
// returns: ok, type, slot, usable, condition, aimed, heldAfter, error, detail
//
// Uses one carried item through the game's own Body.UseItem path — the drag/radial invocation the
// character-sound capture wraps, next to Body.UseItemInHand. `type` is the item id a body slot holds.
// When either aim value is non-zero the local aim first moves to position + (aim-x, aim-y), which is
// the aim a direct-placeable delegate reads; (0, 0) leaves the aim alone (food and container uses read
// no aim). The run provides the item with the item-provide recipe and records the declared substitution.
((System.Func<string>)(() => {
	var type = {{s:type}};
	var aimX = {{n:aim-x}};
	var aimY = {{n:aim-y}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var aimed = false;
	if (aimX != 0f || aimY != 0f) {
		body.targetLookPos = body.transform.position + new Vector3(aimX, aimY, 0f);
		aimed = true;
	}
	Item held = null;
	var slot = -1;
	for (var i = 0; i < body.slots.Length; i++) {
		var candidate = body.GetItem(i);
		if (candidate != null && candidate.id == type) { held = candidate; slot = i; break; }
	}
	if (held == null) { return "{\"ok\":false,\"error\":\"not-held\",\"detail\":\"no body slot holds " + type + "\"}"; }
	var usable = held.Stats != null && held.Stats.usable;
	var condition = held.condition;
	body.UseItem(held);
	return "{\"ok\":true,\"type\":\"" + type + "\""
		+ ",\"slot\":" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"usable\":" + (usable ? "true" : "false")
		+ ",\"condition\":" + condition.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"aimed\":" + (aimed ? "true" : "false")
		+ ",\"heldAfter\":" + (body.HoldingItem(held) ? "true" : "false") + "}";
}))()
