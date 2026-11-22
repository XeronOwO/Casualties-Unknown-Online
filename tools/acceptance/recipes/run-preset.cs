// recipe: run-preset
// args: preset=s
// returns: ok, preset, count, error, detail
//
// Applies one of the game's own run presets to the run-settings table, exactly the way the pre-run screen does:
// it asks RunSettings.GetPreset(name) for the preset object and writes every (key, value) pair of its
// `presetValues` dictionary into WorldGeneration.runSettings. Reflection-only: the game's types are not nameable
// from the eval scope.
//
// This exists because the setting a run starts with is decided by the chosen preset — writing a single key and
// then starting the run is overwritten by the preset the start applies — so a row that wants a specific
// starting-supplies value has to choose the preset a player would choose.
((System.Func<string>)(() => {
	var presetArg = {{s:preset}};
	System.Type settingsType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && settingsType == null; i++) {
		try { settingsType = assemblies[i].GetType("RunSettings", false); } catch { settingsType = null; }
	}
	if (settingsType == null) { return "{\"ok\":false,\"error\":\"no-run-settings-type\"}"; }
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var getPreset = settingsType.GetMethod("GetPreset", flags, null, new System.Type[] { typeof(string) }, null);
	if (getPreset == null) { return "{\"ok\":false,\"error\":\"no-get-preset\"}"; }
	var preset = getPreset.Invoke(null, new object[] { presetArg });
	if (preset == null) { return "{\"ok\":false,\"error\":\"unknown-preset\",\"preset\":\"" + presetArg + "\"}"; }
	var valuesField = preset.GetType().GetField("presetValues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	if (valuesField == null) { return "{\"ok\":false,\"error\":\"no-preset-values\"}"; }
	var values = valuesField.GetValue(preset) as System.Collections.IDictionary;
	if (values == null) { return "{\"ok\":false,\"error\":\"preset-values-not-a-table\"}"; }
	System.Type genType = null;
	for (var i = 0; i < assemblies.Length && genType == null; i++) {
		try { genType = assemblies[i].GetType("WorldGeneration", false); } catch { genType = null; }
	}
	if (genType == null) { return "{\"ok\":false,\"error\":\"no-world-generation-type\"}"; }
	var settingsField = genType.GetField("runSettings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	if (settingsField == null) { return "{\"ok\":false,\"error\":\"no-run-settings-field\"}"; }
	var table = settingsField.GetValue(null) as System.Collections.IDictionary;
	if (table == null) { return "{\"ok\":false,\"error\":\"no-run-settings-table\"}"; }
	var count = 0;
	foreach (System.Collections.DictionaryEntry entry in values) {
		table[entry.Key] = entry.Value;
		count++;
	}
	return "{\"ok\":true,\"preset\":\"" + presetArg + "\",\"count\":" + count + "}";
}))()
