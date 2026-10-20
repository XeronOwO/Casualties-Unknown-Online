// recipe: body-swap
// args: slot1=n slot2=n
// serves: snapshot-overtakes-item-reports (its acceptance row needed the `Body.SwapSlots` half: two
//         OCCUPIED ring slots exchanged through the game's own call, beside `body-call kind=switch` for
//         `Body.SwitchHands`); any row that needs a native slot swap without a drag gesture
// returns: ok, slot1, slot2, type1, id1, type2, id2, applied, error, detail
//
// Drives ONE native `Body.SwapSlots(slot1, slot2)` over two occupied ring slots on the LOCAL body — the
// same call the drag UI's occupied-destination move reaches — so the swap's own report chain runs (the
// owner's two slot facts, then the postfix's snapshot). Both slots are read before the call and both
// occupants are checked back after it, so `applied` is a native outcome, not a request. `slot1` and
// `slot2` must be two different occupied ring slots (`Body.SlotOf`/`GetItem`'s own index space); an empty
// slot is refused by name rather than substituted, because the occupied-destination shape is the point.
// No inner lambda captures a local: the evaluator's Mono REPL refuses that shape (the const flags are
// inlined, which is why the shared id reader stays a delegate over them).
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var slot1 = (int){{n:slot1}};
	var slot2 = (int){{n:slot2}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	if (slot1 < 0 || slot2 < 0 || slot1 >= body.slots.Length || slot2 >= body.slots.Length || slot1 == slot2) {
		return "{\"ok\":false,\"error\":\"bad-slots\",\"detail\":\"slot1 and slot2 must be two different ring slots\"}";
	}
	var idOf = new System.Func<Item, ulong>((probe) => {
		var components = probe.GetComponents<UnityEngine.Component>();
		for (var i = 0; i < components.Length; i++) {
			if (components[i] == null || components[i].GetType().Name != "ItemInstanceId") { continue; }
			var idField = components[i].GetType().GetField("Id", instanceFlags);
			if (idField != null) { return System.Convert.ToUInt64(idField.GetValue(components[i])); }
			var idProperty = components[i].GetType().GetProperty("Id", instanceFlags);
			if (idProperty != null) { return System.Convert.ToUInt64(idProperty.GetValue(components[i], null)); }
			return 0UL;
		}
		return 0UL;
	});
	var first = body.GetItem(slot1);
	var second = body.GetItem(slot2);
	if (first == null || second == null) {
		return "{\"ok\":false,\"error\":\"slot-empty\",\"detail\":\"slot1=" + slot1 + " occupied=" + (first != null ? "true" : "false") + ", slot2=" + slot2 + " occupied=" + (second != null ? "true" : "false") + " — both slots must be occupied before the swap\"}";
	}
	var type1 = first.id;
	var type2 = second.id;
	var id1 = idOf(first);
	var id2 = idOf(second);
	body.SwapSlots(slot1, slot2);
	var applied = System.Object.ReferenceEquals(body.GetItem(slot1), second) && System.Object.ReferenceEquals(body.GetItem(slot2), first);
	var culture = System.Globalization.CultureInfo.InvariantCulture;
	return "{\"ok\":true"
		+ ",\"slot1\":" + slot1.ToString(culture)
		+ ",\"slot2\":" + slot2.ToString(culture)
		+ ",\"type1\":\"" + type1 + "\""
		+ ",\"id1\":\"" + id1.ToString(culture) + "\""
		+ ",\"type2\":\"" + type2 + "\""
		+ ",\"id2\":\"" + id2.ToString(culture) + "\""
		+ ",\"applied\":" + (applied ? "true" : "false") + "}";
}))()
