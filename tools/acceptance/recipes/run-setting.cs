// recipe: run-setting
// args: key=s value=n
// returns: ok, key, value, error, detail
//
// Writes one run-setting float into the game's own run-settings table (WorldGeneration.runSettings, the dictionary
// RunSettings.GetPreset fills and the generator reads through GetRunSettingFloat). The pre-run screen writes the same
// table; this hook exists so a run can be started with a non-default setting without a human at the slider. Float
// entries only. Reflection-only: the game's types are not nameable from the eval scope.
((System.Func<string>)(() => {
	var keyArg = {{s:key}};
	var valueArg = {{n:value}};
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
	if (!table.Contains(keyArg)) { return "{\"ok\":false,\"error\":\"unknown-key\"}"; }
	var current = table[keyArg];
	var kind = current == null ? "null" : current.GetType().Name;
	if (current != null && !(current is System.Single)) { return "{\"ok\":false,\"error\":\"not-a-float-setting\",\"detail\":\"" + kind + "\"}"; }
	table[keyArg] = System.Convert.ToSingle(valueArg);
	var now = ((System.Single)table[keyArg]).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
	return "{\"ok\":true,\"key\":\"" + keyArg + "\",\"value\":\"" + now + "\"}";
}))()
