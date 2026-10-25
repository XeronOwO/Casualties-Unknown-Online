// recipe: drag-cue
// args: none
// returns: ok, showing, targetSteamId, displayName, text, error, detail
//
// Reads the CUO drag-use cue the way the overlay draws it: `IDragUseCueState.Showing` and `TargetSteamId` from the running
// services, then the same two facts the overlay composes into its line — the target's display name and the localized
// `cue.use_on` text. This is the reading `cross-player-drag-use-feedback` row 1 asks for: a cue that NAMES the teammate the
// release would use the item on, and disappears when the cursor leaves.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var cueType = (System.Type)null;
	var uiType = (System.Type)null;
	var locType = (System.Type)null;
	var asms = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < asms.Length; i++) {
		if (cueType == null) { try { cueType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.GameAdapter.IDragUseCueState", false); } catch { cueType = null; } }
		if (uiType == null) { try { uiType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Session.OnlineUiState", false); } catch { uiType = null; } }
		if (locType == null) { try { locType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Localization.LocalizationService", false); } catch { locType = null; } }
	}
	if (cueType == null) { return "{\"ok\":false,\"error\":\"no-cue-state-type\"}"; }
	var cue = services.GetService(cueType);
	if (cue == null) { return "{\"ok\":false,\"error\":\"no-cue-state-service\"}"; }
	var showing = (bool)cueType.GetProperty("Showing").GetValue(cue, null);
	var target = (ulong)cueType.GetProperty("TargetSteamId").GetValue(cue, null);
	var name = "";
	if (uiType != null) {
		var ui = services.GetService(uiType);
		if (ui != null) {
			var m = uiType.GetMethod("DisplayName") ?? uiType.GetMethod("NameOf");
			if (m != null) {
				try {
					var args = m.GetParameters().Length == 1 ? new object[] { target } : new object[] { target.ToString() };
					var answer = m.Invoke(ui, args);
					name = answer == null ? "" : answer.ToString();
				} catch { name = ""; }
			}
		}
	}
	var text = "";
	if (locType != null) {
		var loc = services.GetService(locType);
		if (loc != null) {
			var fmt = locType.GetMethod("Format", new System.Type[] { typeof(string), typeof(object[]) });
			if (fmt != null) {
				try { text = (string)fmt.Invoke(loc, new object[] { "cue.use_on", new object[] { name.Length == 0 ? target.ToString() : name } }); } catch { text = ""; }
			}
		}
	}
	// The same world point the selection compares against: CrossPlayerDragUse.Observe feeds
	// camera.ScreenToWorldPoint(Input.mousePosition) into DragTargetSelection.BeginFrame, so reporting it here is the
	// decisive comparison with a remote's streamed position (|dx| <= halfWidth && |dy| <= halfHeight).
	var mouseWorldX = 0f;
	var mouseWorldY = 0f;
	var camera = Camera.main;
	if (camera != null) {
		var world = camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
		mouseWorldX = world.x;
		mouseWorldY = world.y;
	}
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	// Reproduce DragUseCenter.IsUnderPointer's own clauses so a false cue names WHICH one fails.
	var ringOpen = "unknown";
	var focused = "0";
	var menuScale = "0";
	var leniency = "0";
	var centreDistance = "-1";
	var centreRadius = "-1";
	var viewType = (System.Type)null;
	var viewNames = new string[] {
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteBackpackView",
		"CasualtiesUnknownOnline.GameAdapter.RemoteBackpackView",
		"RemoteBackpackView"
	};
	for (var i = 0; i < asms.Length; i++) {
		for (var n = 0; n < viewNames.Length; n++) {
			if (viewType != null) { break; }
			try { viewType = asms[i].GetType(viewNames[n], false); } catch { viewType = null; }
		}
	}
	if (viewType != null) {
		try {
			var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
			var isOpen = viewType.GetProperty("IsOpen", flags);
			if (isOpen != null) { ringOpen = isOpen.GetValue(null, null).ToString(); } else { ringOpen = "no-IsOpen-property"; }
			var focus = viewType.GetProperty("FocusedSteamId", flags);
			if (focus != null) { focused = focus.GetValue(null, null).ToString(); }
		} catch (System.Exception ex) { ringOpen = "err:" + ex.GetType().Name; }
	} else { ringOpen = "no-view-type"; }
	var cam = PlayerCamera.main;
	if (cam != null) {
		var pcType = typeof(PlayerCamera);
		menuScale = pcType.GetField("radialMenu").GetValue(cam) is UnityEngine.RectTransform menu ? menu.localScale.x.ToString("0.###", inv) : "no-menu";
		leniency = pcType.GetField("inventoryUseLeniency", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).GetValue(null).ToString();
		var circle = pcType.GetField("radialCircle").GetValue(cam) as UnityEngine.UI.Image;
		if (circle != null) {
			var dist = UnityEngine.Vector2.Distance(UnityEngine.Input.mousePosition, circle.transform.position) / PlayerCamera.uiScale;
			centreDistance = dist.ToString("0.###", inv);
			centreRadius = (circle.rectTransform.sizeDelta.x * 0.5f).ToString("0.###", inv);
		} else { centreDistance = "no-circle"; }
	}
	return "{\"ok\":true,\"showing\":" + (showing ? "true" : "false")
		+ ",\"targetSteamId\":\"" + target.ToString() + "\""
		+ ",\"displayName\":\"" + name.Replace("\\", "/").Replace("\"", "'") + "\""
		+ ",\"text\":\"" + (text ?? "").Replace("\\", "/").Replace("\"", "'") + "\""
		+ ",\"mouseWorldX\":" + mouseWorldX.ToString("0.###", inv)
		+ ",\"mouseWorldY\":" + mouseWorldY.ToString("0.###", inv)
		+ ",\"backpackOpen\":\"" + ringOpen + "\""
		+ ",\"focusedSteamId\":\"" + focused + "\""
		+ ",\"menuScale\":\"" + menuScale + "\""
		+ ",\"useLeniency\":\"" + leniency + "\""
		+ ",\"centreDistance\":\"" + centreDistance + "\""
		+ ",\"centreRadius\":\"" + centreRadius + "\"}";
}))()
