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
	// The table holds floats (`RunSettingFloat`) and ints (`RunSettingDropdown`, e.g. `startingsupplies` 0/1/2);
	// write back the same type the entry already has so the generator's own reader sees its expected shape.
	if (current is System.Single) { table[keyArg] = System.Convert.ToSingle(valueArg); }
	else if (current is System.Int32) { table[keyArg] = System.Convert.ToInt32(valueArg); }
	else { return "{\"ok\":false,\"error\":\"not-a-numeric-setting\",\"detail\":\"" + kind + "\"}"; }
	var stored = table[keyArg];
	var now = System.Convert.ToString(stored, System.Globalization.CultureInfo.InvariantCulture) ?? "";
	return "{\"ok\":true,\"key\":\"" + keyArg + "\",\"kind\":\"" + stored.GetType().Name + "\",\"value\":\"" + now + "\"}";
}))()
