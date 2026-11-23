// recipe: run-preset-map
// args: preset=s
// returns: ok, preset, count, values, error, detail
//
// Reads one of the game's own presets as the name→value map the game would write, without touching the
// run: `RunSettings.GetPreset(name)`'s `presetValues` dictionary. A row that applies a preset has to know
// what the preset itself says, otherwise it can only check that some numbers were written. Values are
// reported in the invariant text form the settings table uses. Reflection-only.
((System.Func<string>)(() => {
	var presetArg = {{s:preset}};
	System.Type settingsType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && settingsType == null; i++) {
		try { settingsType = assemblies[i].GetType("RunSettings", false); } catch { settingsType = null; }
	}
	if (settingsType == null) { return "{\"ok\":false,\"error\":\"no-run-settings-type\"}"; }
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var presetsField = settingsType.GetField("presets", flags);
	var presets = presetsField == null ? null : (System.Collections.IEnumerable)presetsField.GetValue(null);
	if (presets == null) { return "{\"ok\":false,\"error\":\"no-presets\"}"; }
	object chosen = null;
	foreach (var preset in presets) {
		if (preset == null) { continue; }
		var label = System.Convert.ToString(preset.GetType().GetField("presetName")?.GetValue(preset)) ?? "";
		if (label == presetArg) { chosen = preset; }
	}
	if (chosen == null) { return "{\"ok\":false,\"error\":\"unknown-preset\",\"preset\":\"" + presetArg + "\"}"; }
	var valuesField = chosen.GetType().GetField("presetValues");
	var values = valuesField == null ? null : (System.Collections.IDictionary)valuesField.GetValue(chosen);
	if (values == null) { return "{\"ok\":false,\"error\":\"no-preset-values\",\"preset\":\"" + presetArg + "\"}"; }
	var text = new System.Text.StringBuilder();
	var count = 0;
	foreach (System.Collections.DictionaryEntry pair in values) {
		var name = System.Convert.ToString(pair.Key) ?? "";
		var value = pair.Value == null ? "" : System.Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
		count++;
		if (text.Length > 0) { text.Append(";"); }
		text.Append(name).Append("=").Append(value);
	}
	var escaped = text.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"");
	return "{\"ok\":true,\"preset\":\"" + presetArg + "\",\"count\":" + count + ",\"values\":\"" + escaped + "\"}";
}))()
