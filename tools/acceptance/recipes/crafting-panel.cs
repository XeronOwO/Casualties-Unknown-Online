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
	if (mode == "probe") {
		// Names the recipes whose own product sprite throws inside RefreshRecipeList (the 1.0 panel
		// cannot open while one such recipe is visible). Reflection only.
		var probeRecipes = findType("Recipes");
		var probeList = probeRecipes == null ? null : probeRecipes.GetField("recipes", F)?.GetValue(null) as System.Collections.IList;
		if (probeList == null) { return "{\"ok\":false,\"error\":\"no-recipes\"}"; }
		var bad = new System.Collections.Generic.List<string>();
		var checkedCount = 0;
		for (var i = 0; i < probeList.Count && checkedCount < 400; i++) {
			var probeRecipe = probeList[i];
			if (probeRecipe == null) { continue; }
			checkedCount++;
			try {
				probeRecipe.GetType().GetProperty("resultSprite", F)?.GetValue(probeRecipe);
			} catch (System.Exception ex) {
				var inner = ex.InnerException ?? ex;
				var probeResult = probeRecipe.GetType().GetField("result", F)?.GetValue(probeRecipe);
				var probeId = probeResult == null ? "<none>" : System.Convert.ToString(probeResult.GetType().GetField("id", F)?.GetValue(probeResult));
				var probeAmount = probeResult == null ? "<none>" : System.Convert.ToString(probeResult.GetType().GetField("amount", F)?.GetValue(probeResult));
				var probeItems = probeRecipe.GetType().GetField("items", F)?.GetValue(probeRecipe) as System.Collections.IList;
				var needs = "";
				if (probeItems != null) {
					for (var n = 0; n < probeItems.Count; n++) {
						var ing = probeItems[n];
						if (ing == null) { continue; }
						var specificId = System.Convert.ToString(ing.GetType().GetField("specificId", F)?.GetValue(ing));
						var specific = System.Convert.ToString(ing.GetType().GetField("specific", F)?.GetValue(ing));
						if (n > 0) { needs += ","; }
						needs += specificId + "|specific=" + specific;
					}
				}
				var needs2 = "";
				needs2 += "|visible=" + System.Convert.ToString(probeRecipe.GetType().GetProperty("visible", F)?.GetValue(probeRecipe));
				needs2 += "|INT=" + System.Convert.ToString(probeRecipe.GetType().GetField("INT", F)?.GetValue(probeRecipe));
				needs2 += "|specialKnown=" + System.Convert.ToString(probeRecipe.GetType().GetField("specialKnown", F)?.GetValue(probeRecipe));
				bad.Add(probeId + "|" + inner.GetType().Name + "|amount=" + probeAmount + "|needs=" + needs + needs2);
			}
		}
		var probeText = "{\"ok\":true,\"checked\":" + checkedCount + ",\"badCount\":" + bad.Count + ",\"badIds\":[";
		for (var b = 0; b < bad.Count && b < 20; b++) { if (b > 0) { probeText += ","; } probeText += "\"" + bad[b].Replace("\\", "/").Replace("\"", "'") + "\""; }
		probeText += "]}";
		return probeText;
	}

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
