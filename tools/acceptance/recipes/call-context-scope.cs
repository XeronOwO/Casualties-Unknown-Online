// recipe: call-context-scope
// args: mode=s
// serves: call-context-scope-leak rows 1 and 2 (the frame that entered a scope and never disposed it, and
//         whether later CallContext.Enter calls still succeed after the game itself threw)
// returns: ok, mode, depth, current, origins, itemId, slot, tags, before, after, error, detail
//
// The amplifier half of batch 20261011-k, made observable instead of read. `CallContext` is CUO's
// per-process call-identity stack: a scope-entering Harmony prefix pushes an origin, its postfix pops it. A
// game-side throw inside the wrapped ORIGINAL skips the postfix, so that entry stays on the stack forever;
// sixteen of them latch every later Enter in that process into refusal, and the client has to be restarted.
//
//   read    — reflect CallContext's own state: the depth and the origins it is holding. The batch's kept
//             artifacts carry none of this: a depth-exceeded stack names the VICTIM frame (the prefix that
//             threw on its own account), while the stack's own contents name the frame that LEAKED.
//   break   — take the local body's hand-slot item and null that definition's derived tag array
//             (`ItemInfo.actualTags`, the private field the game's own table pass fills) through reflection.
//             That restores the defect state the batch's fixture reached by declaring no tags: `Body`'s own
//             per-frame `HandleVisuals` asks `HasTag("gun")` of the hand item and throws, inside CUO's scope
//             around that method, once per frame. The state is the game's to guarantee and this takes it away;
//             nothing here is a CUO input.
//   restore — put the array back through the game's OWN `ItemInfo.SetTags()`. The throws stop, and the second
//             half of the defect shows: the leaked entries are still on the stack, so the client stays broken.
//
// The hand slot matters: `HandleVisuals` asks `HasTag("gun")` of the item THERE, so a carried item in another
// slot is swapped in through the game's own `Body.SwapSlots`, and an empty body is given an item through the
// game's own creation and pickup calls. Read-only apart from the one field the two writing modes name. No
// inner lambda captures a local: the evaluator's Mono REPL refuses that shape.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	if (mode != "read" && mode != "break" && mode != "restore") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be read, break or restore\"}";
	}
	var contextType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.CallContext, CasualtiesUnknownOnline.GameAdapter");
	if (contextType == null) {
		return "{\"ok\":false,\"error\":\"no-call-context\",\"detail\":\"the GameAdapter assembly is not loaded in this process\"}";
	}
	var depthField = contextType.GetField("_depth", statics);
	var stackField = contextType.GetField("Stack", statics);
	if (depthField == null || stackField == null) {
		return "{\"ok\":false,\"error\":\"no-context-state\",\"detail\":\"CallContext no longer carries the private _depth and Stack fields\"}";
	}
	var depth = (int)depthField.GetValue(null);
	var stack = (System.Array)stackField.GetValue(null);
	var origins = "";
	for (var entry = 0; entry < depth && entry < stack.Length; entry++) {
		origins = origins + (entry == 0 ? "" : ",") + stack.GetValue(entry).ToString();
	}
	var current = depth > 0 ? stack.GetValue(depth - 1).ToString() : "LocalAction";
	if (mode == "read") {
		return "{\"ok\":true,\"mode\":\"read\""
			+ ",\"depth\":" + depth.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"current\":\"" + current + "\",\"origins\":\"" + origins + "\"}";
	}
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) {
		return "{\"ok\":false,\"error\":\"no-local-body\",\"depth\":" + depth.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
	}
	var hand = body.GetItem(body.handSlot);
	if (mode == "break" && hand == null) {
		for (var slot = 0; slot < body.slots.Length; slot++) {
			if (slot == body.handSlot || body.GetItem(slot) == null) { continue; }
			body.SwapSlots(slot, body.handSlot);
			hand = body.GetItem(body.handSlot);
			break;
		}
	}
	if (mode == "break" && hand == null) {
		var spawned = Utils.Create("scrapmetal", body.transform.position, 0f);
		if (spawned == null) {
			return "{\"ok\":false,\"error\":\"no-hand-item\",\"detail\":\"the body carries nothing and Utils.Create returned nothing\"}";
		}
		hand = spawned.GetComponent<Item>();
		if (hand == null) {
			UnityEngine.Object.Destroy(spawned);
			return "{\"ok\":false,\"error\":\"no-hand-item\",\"detail\":\"the created object is not an Item\"}";
		}
		body.PickUpItem(hand, body.handSlot, true);
		hand = body.GetItem(body.handSlot);
	}
	if (hand == null) {
		return "{\"ok\":false,\"error\":\"no-hand-item\",\"detail\":\"the hand slot is empty and no carried item could be moved into it\"}";
	}
	ItemInfo info;
	if (!Item.GlobalItems.TryGetValue(hand.id, out info) || info == null) {
		return "{\"ok\":false,\"error\":\"no-definition\",\"detail\":\"Item.GlobalItems carries no definition for " + hand.id + "\"}";
	}
	var tagsField = typeof(ItemInfo).GetField("actualTags", instanceFlags);
	if (tagsField == null) {
		return "{\"ok\":false,\"error\":\"no-tag-field\",\"detail\":\"ItemInfo no longer carries the private actualTags field\"}";
	}
	var before = tagsField.GetValue(info) == null ? "null" : "array";
	if (mode == "restore") {
		info.SetTags();
	}
	else {
		tagsField.SetValue(info, null);
	}
	var after = tagsField.GetValue(info) == null ? "null" : "array";
	return "{\"ok\":true,\"mode\":\"" + mode + "\""
		+ ",\"depth\":" + depth.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"current\":\"" + current + "\",\"origins\":\"" + origins + "\""
		+ ",\"itemId\":\"" + hand.id + "\""
		+ ",\"slot\":" + body.handSlot.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"tags\":\"" + (info.tags == null ? "" : info.tags.Replace("\"", "'")) + "\""
		+ ",\"before\":\"" + before + "\",\"after\":\"" + after + "\""
		+ ",\"applied\":true}";
}))()
