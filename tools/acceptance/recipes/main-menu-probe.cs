// recipe: main-menu-probe
// args: none
// serves: save-layer-end-save-and-restore (row 6: the Continue entry's state)
// returns: ok, hasSave, hasRestorableWorld, preRunScriptFound, loadButtonFound, interactable, imageEnabled, saveTimeText, error, detail
//
// Reads the game's own Continue entry on the main menu WITHOUT the player's eyes: whether the native save
// exists (`SaveSystem.HasSave()`), whether the CUO repository has a world (the adapter's bridge, reflected
// because the recipe compiles outside CUO's assembly) and the live `PreRunScript.loadButton` state the
// player sees (interactable / the icon / the label). The isolating state is `hasSave=false` with a CUO
// world present: the game's own `PreRunScript.Start` sets `loadButton.interactable = SaveSystem.HasSave()`,
// so a lit button in that state can only come from `PreRunScriptStartPatch`'s override.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var hasSave = SaveSystem.HasSave();
	var restorable = "unknown";
	try {
		var bridgeType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.PatchBridge, CasualtiesUnknownOnline.GameAdapter");
		if (bridgeType == null) { restorable = "no-bridge-type"; }
		else {
			var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
			var impl = bridgeType.GetProperty("Impl", flags);
			var value = impl == null ? null : impl.GetValue(null, null);
			if (value == null) { restorable = "no-impl"; }
			else {
				var method = value.GetType().GetMethod("HasRestorableWorld", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				restorable = method == null ? "no-method" : (method.Invoke(value, null) ?? "null").ToString();
			}
		}
	} catch (System.Exception ex) { restorable = "error:" + ex.GetType().Name; }

	var pre = UnityEngine.Object.FindObjectOfType<PreRunScript>();
	var button = pre == null ? null : pre.loadButton;
	var image = pre == null ? null : pre.loadButtonImage;
	var label = pre == null || pre.saveTimeText == null ? "" : pre.saveTimeText.text;
	label = label == null ? "" : label.Replace("\\", "/").Replace("\"", "'").Replace("\r", " ").Replace("\n", " ");
	return "{\"ok\":true"
		+ ",\"hasSave\":" + (hasSave ? "true" : "false")
		+ ",\"hasRestorableWorld\":\"" + restorable + "\""
		+ ",\"preRunScriptFound\":" + (pre != null ? "true" : "false")
		+ ",\"loadButtonFound\":" + (button != null ? "true" : "false")
		+ ",\"interactable\":" + (button == null ? "null" : (button.interactable ? "true" : "false"))
		+ ",\"imageEnabled\":" + (image == null ? "null" : (image.enabled ? "true" : "false"))
		+ ",\"saveTimeText\":\"" + label + "\"}";
}))()
