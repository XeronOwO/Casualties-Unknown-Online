// recipe: ring-buttons
// args: none
// serves: cross-player-drag-use-model-bounds (where a real drag must press, and what the ring offers)
// returns: ok, radialOpen, count, buttons (tag@x,y:slot:itemId), error, detail
//
// Every InvButton in the scene with its screen position, its slot and the item it represents. The drag family's gestures are
// real mouse presses, so the batch needs the pixel of the item it presses on - a control id cannot be pressed by an OS click,
// and a guessed pixel is not a gesture.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	var buttons = UnityEngine.Object.FindObjectsOfType<InvButton>();
	var parts = new System.Collections.Generic.List<string>();
	var count = 0;
	for (var i = 0; i < buttons.Length; i++) {
		var button = buttons[i];
		if (button == null || !button.gameObject.activeInHierarchy) { continue; }
		var rect = button.transform as UnityEngine.RectTransform;
		if (rect == null) { continue; }
		var point = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, rect.position);
		var itemId = "none";
		if (body != null && button.slot >= 0 && body.GetItem(button.slot) != null) { itemId = body.GetItem(button.slot).id; }
		count++;
		parts.Add(button.tag + "@" + ((int)point.x).ToString(inv) + "," + ((int)point.y).ToString(inv) + ":" + button.slot.ToString(inv) + ":" + itemId);
	}
	return "{\"ok\":true,\"radialOpen\":" + (camera.radialOpen ? "true" : "false")
		+ ",\"count\":" + count.ToString(inv)
		+ ",\"buttons\":\"" + string.Join("|", parts) + "\"}";
}))()
