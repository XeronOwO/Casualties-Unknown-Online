// recipe: drag-release
// args: x=n y=n
// returns: ok, x, y, casts, hadItem, after, mode, backpackOpen, focusedSteamId, centreUnderPointer, centreDistance, centreRadius
//
// Releases the drag the game's own bracket holds, at a GAME screen point: the cast list is the EventSystem's real answer at that
// point and `PlayerCamera.HandleReleaseDragging` is the function `Input.GetKeyUp(iteminteract)` calls, so the release runs the same
// branch a player's release does. It also reports the three facts `PlayerCameraDragUsePatch` decides on at that instant — the
// give-gesture MODE, whether a displayed backpack is open and who it names, and whether the pointer is on its USE centre — because
// a release that leaves the item in hand has one of those to blame.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var es = UnityEngine.EventSystems.EventSystem.current;
	if (es == null) { return "{\"ok\":false,\"error\":\"no-event-system\"}"; }

	var asms = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type modeType = null;
	System.Type viewType = null;
	System.Type centreType = null;
	for (var i = 0; i < asms.Length; i++) {
		if (modeType == null) { try { modeType = asms[i].GetType("CasualtiesUnknownOnline.GameAdapter.GiveGestureModePolicy", false); } catch { modeType = null; } }
		if (viewType == null) {
			try { viewType = asms[i].GetType("CasualtiesUnknownOnline.GameAdapter.RemoteBackpackView", false); } catch { viewType = null; }
			if (viewType == null) { try { viewType = asms[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteBackpackView", false); } catch { viewType = null; } }
		}
		if (centreType == null) { try { centreType = asms[i].GetType("CasualtiesUnknownOnline.GameAdapter.DragUseCenter", false); } catch { centreType = null; } }
	}
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = "unknown";
	if (modeType != null) {
		var read = modeType.GetMethod("Read", flags);
		if (read != null) { try { mode = read.Invoke(null, new object[] { camera }).ToString(); } catch { mode = "err"; } }
	}
	var backpackOpen = "unknown";
	var focused = "0";
	if (viewType != null) {
		try {
			var isOpen = viewType.GetProperty("IsOpen", flags);
			if (isOpen != null) { backpackOpen = isOpen.GetValue(null, null).ToString(); }
			var focus = viewType.GetProperty("FocusedSteamId", flags);
			if (focus != null) { focused = focus.GetValue(null, null).ToString(); }
		} catch { backpackOpen = "err"; }
	}
	var centreUnderPointer = "unknown";
	if (centreType != null) {
		var under = centreType.GetMethod("IsUnderPointer", flags);
		if (under != null) { try { centreUnderPointer = under.Invoke(null, new object[] { camera }).ToString(); } catch { centreUnderPointer = "err"; } }
	}
	var centreDistance = "-1";
	var centreRadius = "-1";
	var circle = typeof(PlayerCamera).GetField("radialCircle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).GetValue(camera) as UnityEngine.UI.Image;
	if (circle != null) {
		centreDistance = (UnityEngine.Vector2.Distance(UnityEngine.Input.mousePosition, circle.transform.position) / PlayerCamera.uiScale).ToString("0.###", inv);
		centreRadius = (circle.rectTransform.sizeDelta.x * 0.5f).ToString("0.###", inv);
	}

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
	var release = typeof(PlayerCamera).GetMethod("HandleReleaseDragging", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
	if (release == null) { return "{\"ok\":false,\"error\":\"no-handle-release\"}"; }
	string thrown = "";
	try { release.Invoke(camera, new object[] { casts }); } catch (System.Exception ex) { thrown = ex.GetType().Name + ":" + (ex.InnerException != null ? ex.InnerException.Message : ex.Message); }
	var after = camera.dragItem;
	return "{\"ok\":true,\"x\":" + x.ToString("0.##", inv) + ",\"y\":" + y.ToString("0.##", inv)
		+ ",\"casts\":\"" + names + "\",\"hadItem\":\"" + heldName
		+ "\",\"after\":\"" + (after == null ? "none" : after.name) + "\""
		+ ",\"mode\":\"" + mode + "\""
		+ ",\"backpackOpen\":\"" + backpackOpen + "\""
		+ ",\"focusedSteamId\":\"" + focused + "\""
		+ ",\"centreUnderPointer\":\"" + centreUnderPointer + "\""
		+ ",\"centreDistance\":\"" + centreDistance + "\""
		+ ",\"centreRadius\":\"" + centreRadius + "\""
		+ (thrown.Length == 0 ? "" : ",\"thrown\":\"" + thrown.Replace("\"", "'") + "\"") + "}";
}))()
