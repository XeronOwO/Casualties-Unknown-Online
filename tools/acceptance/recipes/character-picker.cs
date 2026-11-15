// recipe: character-picker
// args: none
// returns: ok, hasInstance, selected, buttons, tooltips, wonchar0, wonchar1, error
//
// Reads the native pre-run picker while the session menu is up: each portrait's Button.interactable
// and UITooltip.enabled, plus the static selection and the solo unlock keys. A session view that
// leaves a locked portrait disabled or tooltipped shows up here as the player sees it.
((System.Func<string>)(() => {
	var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
	System.Type type = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && type == null; i++) { type = assemblies[i].GetType("PreRunScript", false); }
	if (type == null) { return "{\"ok\":false,\"error\":\"no-prerun-type\"}"; }
	object pre = null;
	var instanceProperty = type.GetProperty("instance", flags);
	if (instanceProperty != null) { pre = instanceProperty.GetValue(null, null); }
	if (pre == null) {
		var instanceField = type.GetField("instance", flags);
		if (instanceField != null) { pre = instanceField.GetValue(null); }
	}
	var selectedField = type.GetField("characterSelected", flags);
	var selected = selectedField == null ? -1 : (int)selectedField.GetValue(null);
	var won0 = UnityEngine.PlayerPrefs.GetInt("wonchar0", 0);
	var won1 = UnityEngine.PlayerPrefs.GetInt("wonchar1", 0);
	if (pre == null) {
		return "{\"ok\":true,\"hasInstance\":false,\"selected\":" + selected
			+ ",\"wonchar0\":" + won0 + ",\"wonchar1\":" + won1 + "}";
	}
	var imagesField = type.GetField("charImages", flags);
	var images = imagesField == null ? null : imagesField.GetValue(pre) as System.Collections.IList;
	if (images == null) { return "{\"ok\":false,\"error\":\"no-char-images\",\"hasInstance\":true}"; }
	var buttons = new System.Text.StringBuilder();
	var tooltips = new System.Text.StringBuilder();
	for (var i = 0; i < images.Count; i++) {
		var image = images[i] as UnityEngine.UI.Image;
		var button = image == null ? null : image.GetComponent<UnityEngine.UI.Button>();
		var tooltip = image == null ? null : image.GetComponent<UITooltip>();
		if (i > 0) { buttons.Append(','); tooltips.Append(','); }
		buttons.Append(button != null && button.interactable ? "true" : "false");
		tooltips.Append(tooltip != null && tooltip.enabled ? "true" : "false");
	}
	return "{\"ok\":true,\"hasInstance\":true,\"selected\":" + selected
		+ ",\"buttons\":\"" + buttons + "\""
		+ ",\"tooltips\":\"" + tooltips + "\""
		+ ",\"wonchar0\":" + won0 + ",\"wonchar1\":" + won1 + "}";
}))()
