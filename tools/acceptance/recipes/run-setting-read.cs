// recipe: run-setting-read
// args: key=s
// returns: ok, key, kind, value, error, detail
//
// Reads one entry out of the game's own run-settings table (WorldGeneration.runSettings, the dictionary
// RunSettings.GetPreset fills and the generator reads through GetRunSettingFloat/GetRunSettingInt) without
// writing anything. The value is reported as its own text form plus its runtime kind, so a dropdown and a
// float can be told apart. Reflection-only: the game's types are not nameable from the eval scope.
((System.Func<string>)(() => {
	var keyArg = {{s:key}};
	System.Type genType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && genType == null; i++) {
		try { genType = assemblies[i].GetType("WorldGeneration", false); } catch { genType = null; }
	}
	if (genType == null) { return "{\"ok\":false,\"error\":\"no-world-generation-type\"}"; }
	var settingsField = genType.GetField("runSettings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	if (settingsField == null) { return "{\"ok\":false,\"error\":\"no-run-settings-field\"}"; }
	var settings = settingsField.GetValue(null);
	if (settings == null) { return "{\"ok\":false,\"error\":\"no-run-settings-table\"}"; }
	var table = (System.Collections.IDictionary)settings;
	if (!table.Contains(keyArg)) { return "{\"ok\":false,\"error\":\"unknown-key\",\"key\":\"" + keyArg + "\"}"; }
	var current = table[keyArg];
	var kind = current == null ? "null" : current.GetType().Name;
	var text = current == null ? "" : System.Convert.ToString(current, System.Globalization.CultureInfo.InvariantCulture) ?? "";
	return "{\"ok\":true,\"key\":\"" + keyArg + "\",\"kind\":\"" + kind + "\",\"value\":\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
}))()
