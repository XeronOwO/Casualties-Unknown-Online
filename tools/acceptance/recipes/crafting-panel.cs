// recipe: crafting-panel
// args: mode=s index=n
// serves: recursive-crafting-mod (the one row left: the recipe panel's third state read on screen - the name line the
// panel renders, the marker it carries, and the detail's tooltip line)
// returns: ok, mode, panelActive, selectedIndex, selectedName, detailName, tooltipText, error, detail
//
// Opens the game's own crafting screen through its own entry (`PlayerCamera.OpenCraftScreen`, the method the craft key
// calls) and reads the detail lines the panel renders: child 5 is the TextMeshProUGUI the panel assigns `recipe.fullName`
// into (`PlayerCamera.RefreshCurrentlySelectedRecipe:347`), child 8 the ingredient text. `index=-1` finds the recipe by
// result id: `flimsyknife` is the recursive-crafting fixture (scrapmetal at 0.5 makes it reachable through `rope`).
// Reflection only, never a cast: the recipe's own assembly identities need not be the runtime's.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var index = (int)({{n:index}});
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
	var mainField = cameraType.GetField("main", F);
	var camera = mainField == null ? null : mainField.GetValue(null);
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var panelField = cameraType.GetField("craftingPanel", F);
	var panel = panelField == null ? null : panelField.GetValue(camera) as UnityEngine.GameObject;
	if (panel == null) { return "{\"ok\":false,\"error\":\"no-panel\"}"; }
	var selectedField = cameraType.GetField("selectedRecipe", F);
	var selected = selectedField == null ? -1 : (int)selectedField.GetValue(camera);
	if (mode == "close") {
		if (panel.activeSelf) {
			cameraType.GetMethod("OpenCraftScreen", F).Invoke(camera, null);
		}
	} else if (mode == "open") {
		var recipes = findType("Recipes");
		var listField = recipes == null ? null : recipes.GetField("recipes", F);
		var list = listField == null ? null : listField.GetValue(null) as System.Collections.IList;
		if (list == null) { return "{\"ok\":false,\"error\":\"no-recipes\"}"; }
		if (index < 0) {
			for (var i = 0; i < list.Count; i++) {
				var recipe = list[i];
				if (recipe == null) { continue; }
				var resultField = recipe.GetType().GetField("result", F);
				var recipeResult = resultField == null ? null : resultField.GetValue(recipe);
				var idField = recipeResult == null ? null : recipeResult.GetType().GetField("id", F | System.Reflection.BindingFlags.Public);
				var id = idField == null ? "" : (string)idField.GetValue(recipeResult);
				if (id == "flimsyknife") { index = i; break; }
			}
		}
		if (index < 0) { return "{\"ok\":false,\"error\":\"no-flimsyknife-recipe\"}"; }
		if (!panel.activeSelf) {
			try { cameraType.GetMethod("OpenCraftScreen", F).Invoke(camera, null); }
			catch (System.Exception ex) {
				var inner = ex.InnerException ?? ex;
				var text = inner.GetType().Name + ": " + inner.Message;
				var wv = cameraType.GetField("woundView", F)?.GetValue(camera);
				var bodyField = cameraType.GetField("body", F)?.GetValue(camera);
				var worldType = findType("WorldGeneration");
				var world = worldType?.GetField("world", F | System.Reflection.BindingFlags.Static)?.GetValue(null);
				text += " | woundView=" + (wv == null ? "null" : "set") + " body=" + (bodyField == null ? "null" : "set") + " world=" + (world == null ? "null" : "set");
				var frames = (inner.StackTrace ?? "").Split('\n');
				for (var f = 0; f < frames.Length && f < 3; f++) { text += " | " + frames[f].Trim(); }
				if (text.Length > 300) { text = text.Substring(0, 300); }
				return "{\"ok\":false,\"error\":\"open-threw\",\"detail\":\"" + text.Replace("\\", "/").Replace("\"", "'") + "\"}";
			}
		}
		// The open re-selects a row of the visible category (RefreshRecipeList), so the fixture's selection is set AFTER
		// the open and the panel's own detail refresh is called - the same method the row click calls.
		selectedField.SetValue(camera, index);
		selected = index;
		cameraType.GetMethod("RefreshCurrentlySelectedRecipe", F).Invoke(camera, null);
	}
	var name = "";
	var tooltip = "";
	if (panel.activeSelf) {
		var nameComponent = panel.transform.GetChild(5).GetComponent("TextMeshProUGUI");
		if (nameComponent != null) {
			var textProperty = nameComponent.GetType().GetProperty("text", F);
			name = textProperty == null ? "" : (string)textProperty.GetValue(nameComponent, null);
		}
		var tooltipComponent = panel.transform.GetChild(8).GetComponent("TextMeshProUGUI");
		if (tooltipComponent != null) {
			var textProperty = tooltipComponent.GetType().GetProperty("text", F);
			tooltip = textProperty == null ? "" : (string)textProperty.GetValue(tooltipComponent, null);
		}
	}
	var description = "";
	var recipesType = findType("Recipes");
	var recipesField = recipesType == null ? null : recipesType.GetField("recipes", F);
	var recipeList = recipesField == null ? null : recipesField.GetValue(null) as System.Collections.IList;
	if (recipeList != null && selected >= 0 && selected < recipeList.Count) {
		var selectedRecipe = recipeList[selected];
		var descriptionProperty = selectedRecipe == null ? null : selectedRecipe.GetType().GetProperty("description", F);
		description = descriptionProperty == null ? "" : (string)descriptionProperty.GetValue(selectedRecipe, null);
	}
	var quote = new System.Func<string, string>((value) => value == null ? "" : value.Replace("\\", "\\\\").Replace("\"", "'").Replace("\n", " | ").Replace("\r", ""));
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"panelActive\":" + (panel.activeSelf ? "true" : "false") + ",\"selectedIndex\":" + selected + ",\"detailName\":\"" + quote(name) + "\",\"tooltipText\":\"" + quote(tooltip) + "\",\"descriptionText\":\"" + quote(description) + "\"}";
}))()
