// recipe: crafting-search
// args: query=s
// serves: pinyin-search-standalone-mod (the one row left: the native search box's own look - a pinyin query typed into
// the game's own crafting field narrows the game's own recipe list)
// returns: ok, query, panelActive, fieldText, rowCount, firstRowName, error, detail
//
// Drives the game's own search box: opens the craft screen through `PlayerCamera.OpenCraftScreen`, writes the query into
// both the native field (`recipeFilterField.text`, the box the player sees) and the filter value the refresh reads
// (`PlayerCamera.recipeFilter`), then calls the game's own `RefreshRecipeList`. The list's rows are the `recipeObjects`
// the native refresh creates; their count is the machine number. The pinyin mod's prefix/postfix scope only rewrites the
// name the native `Contains` filter reads, so a pinyin query that matches a Chinese recipe name narrows the same list the
// literal query narrows.
// Reflection only, never a cast: the recipe's own assembly identities need not be the runtime's.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var query = {{s:query}};
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
	var camera = cameraType.GetField("main", F).GetValue(null);
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var panel = cameraType.GetField("craftingPanel", F).GetValue(camera) as UnityEngine.GameObject;
	if (panel == null) { return "{\"ok\":false,\"error\":\"no-panel\"}"; }
	if (!panel.activeSelf) {
		cameraType.GetMethod("OpenCraftScreen", F).Invoke(camera, null);
	}
	var filterField = cameraType.GetField("recipeFilterField", F).GetValue(camera);
	var fieldText = "";
	if (filterField != null) {
		var textProperty = filterField.GetType().GetProperty("text", F);
		if (textProperty != null) {
			textProperty.SetValue(filterField, query, null);
			fieldText = (string)textProperty.GetValue(filterField, null);
		}
	}
	var filterValue = cameraType.GetField("recipeFilter", F);
	if (filterValue != null) {
		filterValue.SetValue(camera, query);
	}
	cameraType.GetMethod("RefreshRecipeList", F).Invoke(camera, null);
	var rows = cameraType.GetField("recipeObjects", F).GetValue(camera) as System.Collections.IList;
	var rowCount = rows == null ? 0 : rows.Count;
	var quote = new System.Func<string, string>((text) => text == null ? "" : text.Replace("\\", "\\\\").Replace("\"", "'").Replace("\n", " | ").Replace("\r", ""));
	return "{\"ok\":true,\"query\":\"" + quote(query) + "\",\"panelActive\":" + (panel.activeSelf ? "true" : "false") + ",\"fieldText\":\"" + quote(fieldText) + "\",\"rowCount\":" + rowCount + "}";
}))()
