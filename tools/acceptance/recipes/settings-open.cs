// recipe: settings-open
// args: tab=n
// returns: ok, reparented, active, selfActive, parent, error, detail
//
// Opens the GAME's own settings menu through its own entry point and puts it under a canvas the game draws: `OpenMenu(null)`
// builds a menu nothing draws, and the singleton then keeps it, so an existing parentless instance is REPARENTED instead of
// destroyed (Destroy is deferred, and the singleton's own OnDestroy fights a manual clear). The parent is the same one the
// game passes - the pause handler's transform, else the pre-run canvas. An optional tab index goes through SelectTab.
((System.Func<string>)(() => {
	var tab = (int){{n:tab}};
	UnityEngine.Transform parent = null;
	if (PauseHandler.main != null) { parent = PauseHandler.main.transform; }
	if (parent == null && PreRunScript.instance != null && PreRunScript.instance.mainCanvas != null) { parent = PreRunScript.instance.mainCanvas.transform; }
	if (parent == null) { return "{\"ok\":false,\"error\":\"no-parent-canvas\"}"; }
	var reparented = false;
	if (SettingsMenu.instance == null) { SettingsMenu.OpenMenu(parent); } else if (SettingsMenu.instance.transform.parent == null) { SettingsMenu.instance.transform.SetParent(parent, false); reparented = true; }
	if (SettingsMenu.instance == null) { return "{\"ok\":false,\"error\":\"no-menu-after-open\"}"; }
	if (tab >= 0) { SettingsMenu.instance.SelectTab(tab); }
	var parentName = SettingsMenu.instance.transform.parent != null ? SettingsMenu.instance.transform.parent.name : "none";
	return "{\"ok\":true,\"reparented\":" + (reparented ? "true" : "false") + ",\"active\":" + (SettingsMenu.instance.gameObject.activeInHierarchy ? "true" : "false") + ",\"selfActive\":" + (SettingsMenu.instance.gameObject.activeSelf ? "true" : "false") + ",\"parent\":\"" + parentName + "\",\"tab\":" + tab.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()