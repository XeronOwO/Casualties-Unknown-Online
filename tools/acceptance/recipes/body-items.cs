// recipe: body-items
// args: none
// serves: cross-player-eat-skips-native-steps (row 4 needs the carried item's remaining condition on each end)
// returns: ok, count, items, error, detail
//
// Every item in the LOCAL body's slots on this frame, as `slot:name:condition` — the game's own `Body.GetItem` and `Item.condition`, so the number is what
// the player would read off the item. `item-row` answers the kernel row's location only and `carry-read` answers bodies, which is why the condition had no
// read before (2026-10-28).
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = GameObject.FindObjectOfType(System.Type.GetType("PlayerCamera, Assembly-CSharp"));
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var cameraType = camera.GetType();
	var bodyField = cameraType.GetField("body");
	var body = bodyField == null ? null : bodyField.GetValue(camera);
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var bodyType = body.GetType();
	var getItem = bodyType.GetMethod("GetItem");
	if (getItem == null) { return "{\"ok\":false,\"error\":\"no-GetItem\"}"; }
	var items = "";
	var count = 0;
	for (var slot = 0; slot < 6; slot++) {
		object item = null;
		try { item = getItem.Invoke(body, new object[] { slot }); } catch { item = null; }
		if (item == null) { continue; }
		var itemType = item.GetType();
		var nameField = itemType.GetField("itemId") ?? itemType.GetField("id");
		var nameValue = nameField == null ? null : nameField.GetValue(item);
		var conditionField = itemType.GetField("condition");
		var conditionValue = conditionField == null ? null : conditionField.GetValue(item);
		var condition = conditionValue is float f ? f.ToString("0.###", inv) : "n/a";
		count++;
		items += (items.Length == 0 ? "" : "|") + slot + ":" + (nameValue == null ? "?" : nameValue.ToString()) + ":" + condition;
	}
	return "{\"ok\":true,\"count\":" + count + ",\"items\":\"" + items.Replace("\"", "'") + "\"}";
}))()
