// recipe: limb-use
// args: type=s
// returns: ok, type, slot, condition, usableOnLimb, actuallyUsable, limb, limbName, skinBefore, error, detail
//
// Uses a carried item on the limb the LOCAL wound view has selected, through the game's own entry for it: `PlayerCamera.ApplyWoundItem(item)`
// (reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs), which is what the wound view's use button runs. It is NOT `Body.UseItem`: that one is
// gated on `item.Stats.usable` and a bandage's stats answer false, so `Body.UseItem` is a silent no-op for the whole limb-treatment family — the same
// reason a local treatment looked impossible from the `item-use` recipe.
//
// `ApplyWoundItem` itself applies the game's own conditions (a dismembered limb returns, the medical cutoff alerts, consciousness and
// `ActuallyUsableOnLimb`), so a refusal here is the GAME's refusal and the answer names which limb was selected and what the item reports.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var type = {{s:type}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-player-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var limb = camera.selectedLimb;
	if (limb == null) { return "{\"ok\":false,\"error\":\"no-selected-limb\"}"; }

	var item = (Item)null;
	var slot = -1;
	for (var i = 0; i < 6; i++) {
		if (!body.HoldingItem(i)) { continue; }
		var candidate = body.GetItem(i);
		if (candidate != null && candidate.id == type) { item = candidate; slot = i; break; }
	}
	if (item == null) { return "{\"ok\":false,\"error\":\"not-held\",\"detail\":\"no body slot holds " + type + "\"}"; }

	var skinBefore = limb.skinHealth;
	var usableOnLimb = item.Stats != null && item.Stats.usableOnLimb;
	var actuallyUsable = item.Stats != null && item.Stats.ActuallyUsableOnLimb(item);
	var limbIndex = System.Array.IndexOf(body.limbs.ToArray(), limb);
	camera.ApplyWoundItem(item);

	return "{\"ok\":true,\"type\":\"" + type + "\""
		+ ",\"slot\":" + slot.ToString(inv)
		+ ",\"condition\":" + item.condition.ToString("0.###", inv)
		+ ",\"usableOnLimb\":" + (usableOnLimb ? "true" : "false")
		+ ",\"actuallyUsable\":" + (actuallyUsable ? "true" : "false")
		+ ",\"limb\":" + limbIndex.ToString(inv)
		+ ",\"limbName\":\"" + limb.fullName + "\""
		+ ",\"skinBefore\":" + skinBefore.ToString("0.###", inv) + "}";
}))()
