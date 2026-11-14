// recipe: run-switch-state
// args: none
// returns: ok, unchipped, setting, depth, blockItemUsage, error
//
// Reads the live switch that the last-layer cutscene consults (WorldGeneration.unchipped), the run-settings
// value it was derived from, the layer depth and the camera's use gate. Both clients must answer the same
// derived switch; a member that started from its own menu table reads false while the host reads true.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags StaticAny = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	System.Type genType = null;
	System.Type camType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		try {
			if (genType == null) { genType = assemblies[i].GetType("WorldGeneration", false); }
			if (camType == null) { camType = assemblies[i].GetType("PlayerCamera", false); }
		} catch { }
	}
	if (genType == null) { return "{\"ok\":false,\"error\":\"no-worldgeneration\"}"; }

	var unchippedProperty = genType.GetProperty("unchipped", StaticAny);
	if (unchippedProperty == null) { return "{\"ok\":false,\"error\":\"no-unchipped-property\"}"; }
	var unchipped = unchippedProperty.GetValue(null, null) is bool flag && flag;

	var worldField = genType.GetField("world", StaticAny);
	var world = worldField == null ? null : worldField.GetValue(null);
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }

	var settingsField = genType.GetField("runSettings", StaticAny);
	var settings = settingsField == null ? null : settingsField.GetValue(null);
	var table = settings as System.Collections.IDictionary;
	object setting = table != null && table.Contains("unchipped") ? table["unchipped"] : null;
	var settingText = setting == null ? "null" : setting.ToString().ToLowerInvariant();

	var depthField = genType.GetField("biomeDepth", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
	var depth = depthField == null ? -1 : System.Convert.ToInt32(depthField.GetValue(world), System.Globalization.CultureInfo.InvariantCulture);

	var blockText = "null";
	if (camType != null) {
		var camField = camType.GetField("main", StaticAny);
		var cam = camField == null ? null : camField.GetValue(null);
		var blockField = camType.GetField("blockItemUsage", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (cam != null && blockField != null && blockField.GetValue(cam) is bool blocked) { blockText = blocked ? "true" : "false"; }
	}

	return "{\"ok\":true,\"unchipped\":" + (unchipped ? "true" : "false")
		+ ",\"setting\":" + settingText
		+ ",\"depth\":" + depth
		+ ",\"blockItemUsage\":" + blockText + "}";
}))()
