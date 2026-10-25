// recipe: drag-release
// args: x=n y=n
// returns: ok, x, y, casts, hadItem, after
//
// Releases the drag the game's own bracket holds, at a GAME screen point: the cast list is the EventSystem's real answer at
// that point and `PlayerCamera.HandleReleaseDragging` is the function `Input.GetKeyUp(iteminteract)` calls, so the release
// runs the same branch a player's release does (drop, use, hand to a body or open that player's backpack).
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
		names += (i == 0 ? "" : "|") + (g == null ? "null" : g.name);
	}
	var held = camera.dragItem;
	var heldName = held == null ? "none" : held.name;
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
	var release = typeof(PlayerCamera).GetMethod("HandleReleaseDragging", flags);
	if (release == null) { return "{\"ok\":false,\"error\":\"no-handle-release\"}"; }
	string thrown = "";
	try { release.Invoke(camera, new object[] { casts }); } catch (System.Exception ex) { thrown = ex.GetType().Name + ":" + (ex.InnerException != null ? ex.InnerException.Message : ex.Message); }
	var after = camera.dragItem;
	return "{\"ok\":true,\"x\":" + x.ToString("0.##", inv) + ",\"y\":" + y.ToString("0.##", inv)
		+ ",\"casts\":\"" + names + "\",\"hadItem\":\"" + heldName
		+ "\",\"after\":\"" + (after == null ? "none" : after.name) + "\""
		+ (thrown.Length == 0 ? "" : ",\"thrown\":\"" + thrown.Replace("\"", "'") + "\"") + "}";
}))()
