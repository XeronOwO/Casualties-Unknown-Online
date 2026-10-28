// recipe: grab-slot
// args: slot=n
// returns: ok, slot, x, y, tried, dragItem, dragItemId, error, detail
//
// Starts the game's own drag bracket on the InvButton that actually CARRIES an item, computing each button's screen point and
// raycasting it in the SAME call. Splitting the two (read the pixel with `ring-buttons`, press it with `drag-start`) fails
// whenever the HUD moves between the two answers, and a slot number is not a reliable handle either: `MainMainHand` — the hand
// display — can sit exactly on a slot's InvButton and answer an empty item (measured 2026-10-29, four presses in a row). So this
// walks the scene's buttons, asks the game for each one's own point, and reports which press produced a drag. `slot` narrows the
// walk (a negative value means "any button").
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var wanted = {{n:slot}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var es = UnityEngine.EventSystems.EventSystem.current;
	if (es == null) { return "{\"ok\":false,\"error\":\"no-event-system\"}"; }
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
	var start = typeof(PlayerCamera).GetMethod("HandleStartDragging", flags);
	if (start == null) { return "{\"ok\":false,\"error\":\"no-handle-start\"}"; }
	var dragField = typeof(PlayerCamera).GetField("dragItem", flags);
	var buttons = UnityEngine.Object.FindObjectsOfType<InvButton>();
	var tried = "";
	for (var i = 0; i < buttons.Length; i++) {
		var button = buttons[i];
		if (button == null || !button.gameObject.activeInHierarchy) { continue; }
		if (wanted >= 0 && button.slot != wanted) { continue; }
		var rect = button.transform as UnityEngine.RectTransform;
		if (rect == null) { continue; }
		if (button.GetItem() == null) { continue; }
		var point = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, rect.position);
		var ped = new UnityEngine.EventSystems.PointerEventData(es);
		ped.position = new UnityEngine.Vector2(point.x, point.y);
		var casts = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
		es.RaycastAll(ped, casts);
		var names = "";
		for (var c = 0; c < casts.Count; c++) {
			var g = casts[c].gameObject;
			names += (c == 0 ? "" : "|") + (g == null ? "null" : g.name);
		}
		tried += (tried.Length == 0 ? "" : " ") + button.slot.ToString(inv) + "@" + ((int)point.x).ToString(inv) + "," + ((int)point.y).ToString(inv) + "[" + names + "]";
		var args = new object[] { casts, null };
		try { start.Invoke(camera, args); } catch { }
		var dragItem = dragField == null ? null : dragField.GetValue(camera);
		if (dragItem == null) { continue; }
		var itemType = dragItem.GetType();
		var idField = itemType.GetField("id");
		var dragId = idField == null ? "?" : (idField.GetValue(dragItem) == null ? "?" : idField.GetValue(dragItem).ToString());
		return "{\"ok\":true,\"slot\":" + button.slot.ToString(inv)
			+ ",\"x\":" + ((int)point.x).ToString(inv) + ",\"y\":" + ((int)point.y).ToString(inv)
			+ ",\"tried\":\"" + tried.Replace("\"", "'") + "\",\"dragItem\":\"" + dragItem.ToString().Replace("\"", "'") + "\",\"dragItemId\":\"" + dragId + "\"}";
	}
	return "{\"ok\":false,\"error\":\"no-item-button\",\"tried\":\"" + tried.Replace("\"", "'") + "\"}";
}))()
