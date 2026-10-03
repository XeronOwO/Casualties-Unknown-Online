// recipe: craft-drive
// args: mode=s index=n
// serves: item-creation-registration-first row 7 (the crafting regression: one real craft, its creation registered)
// returns: ok, mode, index, craftable, result, int, visible, madeBefore, materials, materialsAvailable, beforeCount, afterCount, products, crafted, reason, error, detail
//
// Drives the game's own crafting entry — Recipes.recipes[index].TryMake(), the exact call
// PlayerCamera.TryCraft makes — with the game's own material matching (GetItemsForRecipe scans the
// body and the world items within 10 units under the native pickup check). mode=list reports every
// recipe whose materials are available with the matched material types; mode=make runs one and
// reports the new carried product ids — the LOCAL half of the craft report only: whether those ids
// reached the host's table is read by the run with container-read mode=host, never assumed here.
// materialsAvailable and reason keep a no-product outcome honest (a full inventory drops the result
// on the ground instead of failing the craft). The id reader is a delegate over the const flags
// only: the evaluator's Mono REPL refuses a delegate that captures a local. One eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var mode = {{s:mode}};
	var index = (int)({{n:index}});
	var recipes = Recipes.recipes;
	if (recipes == null || recipes.Count == 0) { return "{\"ok\":false,\"error\":\"no-recipes\",\"detail\":\"Recipes.recipes is not built\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var idOf = new System.Func<Item, ulong>(delegate(Item probe) {
		var components = probe.GetComponents<UnityEngine.Component>();
		for (var c = 0; c < components.Length; c++) {
			if (components[c] == null || components[c].GetType().Name != "ItemInstanceId") { continue; }
			var idField = components[c].GetType().GetField("Id", flags);
			if (idField != null) { return System.Convert.ToUInt64(idField.GetValue(components[c])); }
			var idProperty = components[c].GetType().GetProperty("Id", flags);
			if (idProperty != null) { return System.Convert.ToUInt64(idProperty.GetValue(components[c], null)); }
			return 0UL;
		}
		return 0UL;
	});
	if (mode == "list") {
		var sb = new System.Text.StringBuilder();
		var craftable = 0;
		for (var i = 0; i < recipes.Count && craftable < 24; i++) {
			var recipe = recipes[i];
			if (recipe == null) { continue; }
			var materials = recipe.GetItemsForRecipe();
			if (materials == null) { continue; }
			if (craftable > 0) { sb.Append(','); }
			craftable = craftable + 1;
			sb.Append("{\"index\":").Append(i.ToString(inv));
			sb.Append(",\"result\":\"").Append(recipe.result.id).Append('"');
			sb.Append(",\"int\":").Append(recipe.INT.ToString(inv));
			sb.Append(",\"visible\":").Append(recipe.visible ? "true" : "false");
			sb.Append(",\"madeBefore\":").Append(recipe.hasMadeBefore ? "true" : "false");
			sb.Append(",\"materials\":[");
			for (var m = 0; m < materials.Count; m++) {
				if (m > 0) { sb.Append(','); }
				sb.Append("{\"type\":\"").Append(materials[m].id).Append("\"}");
			}
			sb.Append("]}");
		}
		return "{\"ok\":true,\"mode\":\"list\",\"craftable\":" + craftable.ToString(inv)
			+ ",\"materials\":[" + sb.ToString() + "]}";
	}
	if (mode != "make") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be list or make\"}"; }
	if (index < 0 || index >= recipes.Count) {
		return "{\"ok\":false,\"error\":\"bad-index\",\"detail\":\"index must name a recipe in Recipes.recipes (0.." + (recipes.Count - 1).ToString(inv) + ")\"}";
	}
	var recipeToMake = recipes[index];
	if (recipeToMake == null) { return "{\"ok\":false,\"error\":\"empty-recipe\"}"; }
	var materialsAvailable = recipeToMake.GetItemsForRecipe() != null;
	var before = body.GetAllItemsThorough();
	var sbProducts = new System.Text.StringBuilder();
	var products = 0;
	var makeDetail = "";
	try { recipeToMake.TryMake(); }
	catch (System.Exception e) { makeDetail = (e.GetType().Name + ": " + e.Message).Replace("\\", "/").Replace("\"", "'"); }
	if (makeDetail.Length != 0) { return "{\"ok\":false,\"error\":\"make-failed\",\"detail\":\"" + makeDetail + "\"}"; }
	var after = body.GetAllItemsThorough();
	for (var i = 0; i < after.Count; i++) {
		var carried = after[i];
		if (carried == null || before.Contains(carried)) { continue; }
		if (products > 0) { sbProducts.Append(','); }
		products = products + 1;
		sbProducts.Append("{\"id\":\"").Append(idOf(carried).ToString(inv)).Append('"');
		sbProducts.Append(",\"type\":\"").Append(carried.id).Append("\"}");
	}
	var reason = products > 0
		? ""
		: (materialsAvailable
			? "no product was picked up (the result may have landed on the ground: every slot full, or the recipe has no autopickup)"
			: "GetItemsForRecipe found no complete material set - run mode=list and pass a craftable recipe index");
	return "{\"ok\":true,\"mode\":\"make\",\"index\":" + index.ToString(inv)
		+ ",\"result\":\"" + recipeToMake.result.id + "\""
		+ ",\"int\":" + recipeToMake.INT.ToString(inv)
		+ ",\"visible\":" + (recipeToMake.visible ? "true" : "false")
		+ ",\"madeBefore\":" + (recipeToMake.hasMadeBefore ? "true" : "false")
		+ ",\"materialsAvailable\":" + (materialsAvailable ? "true" : "false")
		+ ",\"beforeCount\":" + before.Count.ToString(inv)
		+ ",\"afterCount\":" + after.Count.ToString(inv)
		+ ",\"crafted\":" + (products > 0 ? "true" : "false")
		+ ",\"reason\":\"" + reason + "\""
		+ ",\"products\":[" + sbProducts.ToString() + "]}";
}))()
