// recipe: open-context-menu
// args: target=s x=n y=n
// serves: remote-context-menu-medical-visible-when-target-not-visible (its two rows), and any row about what the right-click
//         menu offers
// returns: ok, opened, target, actions, error, detail
//
// Opens the PRODUCTION right-click menu (`OnlineUiPlayerContextMenu.Open`) for one member id, exactly as the world's right-click
// does, at a chosen GUI point, and reports the action ids the menu built for that target. A frame captured right after shows what
// the player would see; the action list is the model the menu draws from, so a row can be judged from both at once.
//
// The menu is a private field of the overlay component, the overlay is a component the plugin adds, and the target is a steam id
// the batch passes: every step is by name and refuses loudly rather than drawing a menu for the wrong target.
((System.Func<string>)(() => {
	var targetText = {{s:target}};
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	ulong target;
	if (!ulong.TryParse(targetText, out target)) { return "{\"ok\":false,\"error\":\"bad-target\",\"detail\":\"" + targetText + "\"}"; }
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type pluginType = null;
	System.Type overlayType = null;
	System.Type menuType = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (pluginType == null) { try { pluginType = assemblies[i].GetType("CasualtiesUnknownOnline.Plugin", false); } catch { pluginType = null; } }
		if (overlayType == null) { try { overlayType = assemblies[i].GetType("CasualtiesUnknownOnline.OnlineUiOverlay", false); } catch { overlayType = null; } }
		if (menuType == null) { try { menuType = assemblies[i].GetType("CasualtiesUnknownOnline.OnlineUiPlayerContextMenu", false); } catch { menuType = null; } }
	}
	if (pluginType == null) { return "{\"ok\":false,\"error\":\"no-plugin-type\"}"; }
	if (overlayType == null) { return "{\"ok\":false,\"error\":\"no-overlay-type\"}"; }
	if (menuType == null) { return "{\"ok\":false,\"error\":\"no-menu-type\"}"; }
	var plugin = UnityEngine.Object.FindObjectOfType(pluginType);
	if (plugin == null) {
		var all = UnityEngine.Resources.FindObjectsOfTypeAll(pluginType);
		if (all != null && all.Length > 0) { plugin = all[0]; }
	}
	if (plugin == null) { return "{\"ok\":false,\"error\":\"no-plugin-instance\"}"; }
	var gameObject = ((UnityEngine.Component)plugin).gameObject;
	// The overlay is a plain class held by the OnlineUiHost COMPONENT (OnlineUiHost constructs it), so the walk goes
	// plugin object -component-> host -field-> overlay -field-> menu.
	System.Type uiHostType = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (uiHostType == null) { try { uiHostType = assemblies[i].GetType("CasualtiesUnknownOnline.OnlineUiHost", false); } catch { uiHostType = null; } }
	}
	if (uiHostType == null) { return "{\"ok\":false,\"error\":\"no-ui-host-type\"}"; }
	object uiHost = gameObject.GetComponent(uiHostType);
	if (uiHost == null) {
		// OnlineUiHost can be a plain class the PluginHost COMPONENT holds, so walk the plugin's components and their fields for
		// whatever object is of that type; the walk is by TYPE, never by a guessed field name.
		var components = ((UnityEngine.Component)plugin).gameObject.GetComponents(typeof(UnityEngine.Component));
		for (var c = 0; c < components.Length && uiHost == null; c++) {
			var componentFields = components[c].GetType().GetFields(flags);
			for (var f = 0; f < componentFields.Length; f++) {
				var value = componentFields[f].GetValue(components[c]);
				if (value != null && uiHostType.IsInstanceOfType(value)) { uiHost = value; break; }
			}
		}
	}
	if (uiHost == null) {
		var hosts = UnityEngine.Resources.FindObjectsOfTypeAll(uiHostType);
		if (hosts != null && hosts.Length > 0) { uiHost = hosts[0]; }
	}
	if (uiHost == null) { return "{\"ok\":false,\"error\":\"no-ui-host-component\"}"; }
	var overlayField = uiHostType.GetField("_onlineUi", flags);
	if (overlayField == null) { return "{\"ok\":false,\"error\":\"no-overlay-field\"}"; }
	var overlay = overlayField.GetValue(uiHost);
	if (overlay == null) { return "{\"ok\":false,\"error\":\"no-overlay-instance\"}"; }
	var menuField = overlayType.GetField("_contextMenu", flags);
	if (menuField == null) { return "{\"ok\":false,\"error\":\"no-menu-field\"}"; }
	var menu = menuField.GetValue(overlay);
	if (menu == null) { return "{\"ok\":false,\"error\":\"no-menu-instance\"}"; }
	var open = menuType.GetMethod("Open", flags);
	if (open == null) { return "{\"ok\":false,\"error\":\"no-open-method\"}"; }
	var candidates = new System.Collections.Generic.List<ulong>();
	candidates.Add(target);
	var position = new UnityEngine.Vector2((float)({{n:x}}), (float)({{n:y}}));
	open.Invoke(menu, new object[] { target, candidates, position });
	var isOpen = (bool)menuType.GetProperty("IsOpen", flags).GetValue(menu, null);
	return "{\"ok\":true,\"opened\":" + (isOpen ? "true" : "false") + ",\"target\":" + target.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
