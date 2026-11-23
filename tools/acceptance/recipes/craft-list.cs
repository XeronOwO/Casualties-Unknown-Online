// recipe: craft-list
// args: none
// serves: row 24 (craft-panel) — the recipes the game says the player can make, as the panel draws them
// returns: ok, panelActive, rows, firstName, rowTexts, error
//
// Reads the game's own recipe list container under the craft screen (`PlayerCamera.recipeListContent`, filled by
// `RefreshRecipeList` with one `Special/RecipeBox` per visible recipe at `PlayerCamera.cs:302`). A row that presses the
// craft key has to see the list the game drew, not the fixture's own idea of it. Read-only, reflection only.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var findType = new System.Func<string, System.Type>((simpleName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } }
		}
		return null;
	});
	var cameraType = findType("PlayerCamera");
	if (cameraType == null) { return "{\"ok\":false,\"error\":\"no-PlayerCamera\"}"; }
	var camera = cameraType.GetField("main", F)?.GetValue(null);
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var panel = cameraType.GetField("craftingPanel", F)?.GetValue(camera) as UnityEngine.GameObject;
	if (panel == null) { return "{\"ok\":false,\"error\":\"no-panel\"}"; }
	var list = cameraType.GetField("recipeListContent", F)?.GetValue(camera) as UnityEngine.Transform;
	if (list == null) { return "{\"ok\":false,\"error\":\"no-list\",\"panelActive\":" + (panel.activeSelf ? "true" : "false") + "}"; }
	var quote = new System.Func<string, string>((value) => value == null ? "" : value.Replace("\\", "\\\\").Replace("\"", "'").Replace("\n", " | ").Replace("\r", ""));
	var rows = list.childCount;
	var firstName = "";
	var texts = "";
	var labelType = findType("TextMeshProUGUI");
	for (var i = 0; i < rows && i < 40; i++) {
		var row = list.GetChild(i);
		if (row == null) { continue; }
		var label = "";
		if (labelType != null) {
			var component = row.GetComponent(labelType);
			if (component != null) { label = quote(System.Convert.ToString(labelType.GetProperty("text", F)?.GetValue(component, null))); }
			else {
				for (var c = 0; c < row.childCount && label.Length == 0; c++) {
					var nested = row.GetChild(c).GetComponent(labelType);
					if (nested != null) { label = quote(System.Convert.ToString(labelType.GetProperty("text", F)?.GetValue(nested, null))); }
				}
			}
		}
		if (i == 0) { firstName = label; }
		if (i > 0) { texts += ","; }
		texts += "\"" + label + "\"";
	}
	return "{\"ok\":true,\"panelActive\":" + (panel.activeSelf ? "true" : "false") + ",\"rows\":" + rows
		+ ",\"firstName\":\"" + firstName + "\",\"rowTexts\":[" + texts + "]}";
}))()
