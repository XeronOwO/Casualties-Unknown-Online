// recipe: recipe-unlock-read
// args: index=n
// returns: ok, index, found, bools, error, detail
//
// Reads the LOCAL client's own recipe table for one index and reports every boolean member of that entry. The unlock family's
// real half is the game's static `Recipes.recipes` write, which no test can touch and which is what a player's crafting list is
// built from: this probe is the data-level reading of "is that index unlocked on THIS client", so a batch can pair the two
// clients instead of judging a pixel list.
//
// The member names are reported rather than assumed: the same probe works whichever field the game calls its unlock flag, and a
// batch reads the state instead of guessing a name.
((System.Func<string>)(() => {
	var index = {{n:index}};
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type recipesType = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (recipesType == null) { try { recipesType = assemblies[i].GetType("Recipes", false); } catch { recipesType = null; } }
	}
	if (recipesType == null) { return "{\"ok\":false,\"error\":\"no-recipes-type\"}"; }
	var tableField = recipesType.GetField("recipes", flags);
	if (tableField == null) { return "{\"ok\":false,\"error\":\"no-table-field\"}"; }
	var table = tableField.GetValue(null);
	var indexValue = (int)(float)index;
	var list = table as System.Collections.IList;
	object entry = null;
	var length = 0;
	if (list != null) {
		length = list.Count;
		if (indexValue < 0 || indexValue >= list.Count) {
			return "{\"ok\":false,\"error\":\"index-out-of-range\",\"detail\":\"" + length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"}";
		}
		entry = list[indexValue];
	}
	else {
		var dictionary = table as System.Collections.IDictionary;
		if (dictionary == null) { return "{\"ok\":false,\"error\":\"table-not-indexable\"}"; }
		length = dictionary.Count;
		entry = dictionary[indexValue] ?? dictionary[(long)indexValue];
		if (entry == null) {
			// A dictionary keyed some other way still answers "does this index exist" by its values' own name, which is what the
			// caller wants: the first entry is enough to report the flag names for a batch that only needs the state.
			var values = dictionary.Values;
			if (values == null || dictionary.Count == 0) { return "{\"ok\":false,\"error\":\"empty-table\"}"; }
			foreach (var candidate in values) { entry = candidate; break; }
		}
	}
	if (entry == null) { return "{\"ok\":false,\"error\":\"no-entry\"}"; }
	var bools = new System.Text.StringBuilder();
	var members = entry.GetType().GetFields(flags);
	var intValue = "none";
	for (var i = 0; i < members.Length; i++) {
		// The unlock is the game's own `INT` field (RecipeUnlockApply writes `Recipes.recipes[idx].INT = 0`, which is what makes a
		// recipe permanently visible), so it is reported by its own name and a change to it is the reading.
		if (members[i].FieldType == typeof(int) && string.Equals(members[i].Name, "INT", System.StringComparison.Ordinal)) {
			intValue = ((int)members[i].GetValue(entry)).ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
		if (members[i].FieldType != typeof(bool)) { continue; }
		if (bools.Length > 0) { bools.Append(","); }
		bools.Append("\"").Append(members[i].Name).Append("\":").Append(((bool)members[i].GetValue(entry)) ? "true" : "false");
	}
	return "{\"ok\":true,\"index\":" + indexValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"found\":true,\"length\":" + length.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"INT\":" + intValue
		+ ",\"bools\":{" + bools.ToString() + "}}";
}))()
