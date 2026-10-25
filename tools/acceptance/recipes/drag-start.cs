// recipe: drag-start
// args: x=n y=n
// returns: ok, x, y, casts, dragItem, dragItemId
//
// Starts the game's OWN drag bracket at a GAME screen point, the way a player's right-press does on the frame it lands:
// `EventSystem.RaycastAll` is run at that point with a real PointerEventData, and the resulting list is handed to
// `PlayerCamera.HandleStartDragging`, which is the exact function `Input.GetKeyDown(iteminteract)` calls. Nothing is
// fabricated: the cast list is the EventSystem's own answer at the real position, and the pickup is the game's own code.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var es = UnityEngine.EventSystems.EventSystem.current;
	if (es == null) { return "{\"ok\":false,\"error\":\"no-event-system\"}"; }
	var ped = new UnityEngine.EventSystems.PointerEventData(es);
	ped.position = new UnityEngine.Vector2(x, y);
	var casts = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
	es.RaycastAll(ped, casts);
	var names = "";
	for (var i = 0; i < casts.Count; i++) {
		var g = casts[i].gameObject;
		if (g == null) { names += (i == 0 ? "" : "|") + "null"; continue; }
		var invButton = g.GetComponent<InvButton>();
		string detail;
		if (invButton == null) {
			detail = "noInvButton";
		} else {
			var held = invButton.GetItem();
			detail = "inv:" + (held == null ? "empty" : held.name) + ":overlaps=" + invButton.Overlaps(casts);
		}
		names += (i == 0 ? "" : "|") + g.name + "#" + detail;
	}
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
	var start = typeof(PlayerCamera).GetMethod("HandleStartDragging", flags);
	if (start == null) { return "{\"ok\":false,\"error\":\"no-handle-start\",\"casts\":\"" + names + "\"}"; }
	var args = new object[] { casts, null };
	string thrown = "";
	try { start.Invoke(camera, args); } catch (System.Exception ex) { thrown = ex.GetType().Name + ":" + (ex.InnerException != null ? ex.InnerException.Message : ex.Message); }
	var item = camera.dragItem;
	var itemId = item == null ? "none" : "yes";
	return "{\"ok\":true,\"x\":" + x.ToString("0.##", inv) + ",\"y\":" + y.ToString("0.##", inv)
		+ ",\"casts\":\"" + names + "\",\"dragItem\":\"" + (item == null ? "none" : item.name)
		+ "\",\"dragItemId\":\"" + itemId + "\""
		+ (thrown.Length == 0 ? "" : ",\"thrown\":\"" + thrown.Replace("\"", "'") + "\"") + "}";
}))()
