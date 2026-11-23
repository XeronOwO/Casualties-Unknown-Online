// recipe: run-settings-list
// args: none
// returns: ok, count, names, presets, presetsProblem, error, detail
//
// Reads the game's own run-settings catalogue without writing anything: the fixed list of
// `RunSetting` entries the pre-run screen offers and the names of the presets it can apply
// (`RunSettings.settingTypes`, `RunSettings.cs:21`, and `RunSettings.presets`, `:213`). A row that
// wants to judge "the settings I pick are the ones the run uses" has to start from these two lists;
// a count of our own keys says nothing about either. Reflection-only: the game's types are not
// nameable from the eval scope.
((System.Func<string>)(() => {
	System.Type settingsType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && settingsType == null; i++) {
		try { settingsType = assemblies[i].GetType("RunSettings", false); } catch { settingsType = null; }
	}
	if (settingsType == null) { return "{\"ok\":false,\"error\":\"no-run-settings-type\"}"; }
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var typesField = settingsType.GetField("settingTypes", flags);
	var presetsField = settingsType.GetField("presets", flags);
	var types = typesField == null ? null : (System.Collections.IEnumerable)typesField.GetValue(null);
	if (types == null) { return "{\"ok\":false,\"error\":\"no-setting-types\"}"; }
	var names = new System.Text.StringBuilder();
	var count = 0;
	foreach (var entry in types) {
		if (entry == null) { continue; }
		var field = entry.GetType().GetField("name", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		var name = field == null ? "" : System.Convert.ToString(field.GetValue(entry)) ?? "";
		count++;
		if (names.Length < 600) { if (names.Length > 0) { names.Append(","); } names.Append(name); }
	}
	var presetNames = new System.Text.StringBuilder();
	var presetsProblem = "";
	var presets = presetsField == null ? null : (System.Collections.IEnumerable)presetsField.GetValue(null);
	if (presets == null) { presetsProblem = "no-presets"; }
	else {
		foreach (var preset in presets) {
			var label = preset == null ? "" : System.Convert.ToString(preset.GetType().GetField("presetName")?.GetValue(preset)) ?? "";
			if (label.Length > 0) { if (presetNames.Length > 0) { presetNames.Append(","); } presetNames.Append(label); }
		}
	}
	return "{\"ok\":true,\"count\":" + count + ",\"names\":\"" + names.ToString() + "\",\"presets\":\"" + presetNames.ToString() + "\",\"presetsProblem\":\"" + presetsProblem + "\"}";
}))()
