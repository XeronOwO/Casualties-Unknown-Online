// recipe: ui-text
// args: none
// serves: console-command-replies-bilingual (its in-game read) and localization-json-resources-and-mod-languages (step 7)
// returns: ok, language, supported, tabHome, consoleReady, prefsLanguage, consoleLines, lastKey, lastText, error, detail
//
// Reads the LIVE localization service inside the running client and what it answers for the keys the UI shows, so the
// acceptance can cite the deployed client's own values instead of a frame: the plugin instance is found by type name, its host's
// service provider is walked by reflection, and the service is asked for the same keys the window renders. `consoleLines` and the
// last console line's key/text come from the command console service when it is reachable, which is the data the chat box prints.
//
// Reflection is unavoidable here (the plugin's fields are private and the assemblies are loaded by BepInEx), and every lookup is
// by name with a named refusal, so a renamed type fails loudly instead of answering a default.
((System.Func<string>)(() => {
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type pluginType = null;
	System.Type hostType = null;
	System.Type localizationType = null;
	System.Type consoleType = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (pluginType == null) { try { pluginType = assemblies[i].GetType("CasualtiesUnknownOnline.Plugin", false); } catch { pluginType = null; } }
		if (hostType == null) { try { hostType = assemblies[i].GetType("CasualtiesUnknownOnline.PluginHost", false); } catch { hostType = null; } }
		if (localizationType == null) { try { localizationType = assemblies[i].GetType("CasualtiesUnknownOnline.Runtime.Localization.ILocalizationService", false); } catch { localizationType = null; } }
		if (consoleType == null) { try { consoleType = assemblies[i].GetType("CasualtiesUnknownOnline.Runtime.Session.Commands.CommandConsoleService", false); } catch { consoleType = null; } }
	}
	if (pluginType == null) { return "{\"ok\":false,\"error\":\"no-plugin-type\"}"; }
	var plugin = UnityEngine.Object.FindObjectOfType(pluginType);
	if (plugin == null) {
		// A plugin component can sit on an inactive object (BepInEx can create it before the scene it lives in is active), and
		// FindObjectOfType only sees active objects: Resources.FindObjectsOfTypeAll sees every loaded one.
		var all = UnityEngine.Resources.FindObjectsOfTypeAll(pluginType);
		if (all != null && all.Length > 0) { plugin = all[0]; }
	}
	if (plugin == null) { return "{\"ok\":false,\"error\":\"no-plugin-instance\"}"; }
	var provider = (System.IServiceProvider)null;
	// The plugin adds its host as a COMPONENT on the same GameObject (Plugin.Awake), so the host is not a field: ask the object.
	if (hostType != null) {
		var hostComponent = ((UnityEngine.Component)plugin).GetComponent(hostType);
		if (hostComponent != null) {
			var hostFields = hostType.GetFields(flags);
			for (var j = 0; j < hostFields.Length; j++) {
				var candidate = hostFields[j].GetValue(hostComponent) as System.IServiceProvider;
				if (candidate != null) { provider = candidate; break; }
			}
		}
	}
	var fields = plugin.GetType().GetFields(flags);
	for (var i = 0; i < fields.Length && provider == null; i++) {
		var value = fields[i].GetValue(plugin);
		if (value == null) { continue; }
		if (hostType != null && hostType.IsInstanceOfType(value)) {
			var hostFields = hostType.GetFields(flags);
			for (var j = 0; j < hostFields.Length; j++) {
				var candidate = hostFields[j].GetValue(value) as System.IServiceProvider;
				if (candidate != null) { provider = candidate; break; }
			}
		}
		if (provider == null && value is System.IServiceProvider direct) { provider = direct; }
	}
	if (provider == null) { return "{\"ok\":false,\"error\":\"no-service-provider\"}"; }
	if (localizationType == null) { return "{\"ok\":false,\"error\":\"no-localization-type\"}"; }
	var localization = provider.GetService(localizationType);
	if (localization == null) { return "{\"ok\":false,\"error\":\"no-localization-service\"}"; }
	var language = (string)localizationType.GetProperty("Language").GetValue(localization, null);
	var supported = (System.Collections.IEnumerable)localizationType.GetProperty("SupportedLanguages").GetValue(localization, null);
	var supportedText = new System.Text.StringBuilder();
	if (supported != null) { foreach (var entry in supported) { if (supportedText.Length > 0) { supportedText.Append(","); } supportedText.Append(entry); } }
	var t = localizationType.GetMethod("T", new System.Type[] { typeof(string) });
	var tabHome = (string)t.Invoke(localization, new object[] { "tab.home" });
	var consoleReady = (string)t.Invoke(localization, new object[] { "console.ready" });
	var prefsLanguage = (string)t.Invoke(localization, new object[] { "prefs.language" });
	var consoleLines = -1;
	var lastKey = "none";
	var lastText = "none";
	if (consoleType != null) {
		var console = provider.GetService(consoleType);
		if (console != null) {
			var lines = (System.Collections.IList)consoleType.GetProperty("Lines").GetValue(console, null);
			if (lines != null) {
				consoleLines = lines.Count;
				if (lines.Count > 0) {
					var last = lines[lines.Count - 1];
					var keyProperty = last.GetType().GetProperty("Key");
					var textProperty = last.GetType().GetProperty("Text");
					if (keyProperty != null && keyProperty.GetValue(last, null) is string key) { lastKey = key; }
					if (textProperty != null && textProperty.GetValue(last, null) is string text) { lastText = text; }
				}
			}
		}
	}
	return "{\"ok\":true,\"language\":\"" + language + "\""
		+ ",\"supported\":\"" + supportedText.ToString() + "\""
		+ ",\"tabHome\":\"" + tabHome.Replace("\"", "'") + "\""
		+ ",\"consoleReady\":\"" + consoleReady.Replace("\"", "'") + "\""
		+ ",\"prefsLanguage\":\"" + prefsLanguage.Replace("\"", "'") + "\""
		+ ",\"consoleLines\":" + consoleLines.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"lastKey\":\"" + lastKey + "\""
		+ ",\"lastText\":\"" + lastText.Replace("\"", "'").Replace("\n", " ") + "\"}";
}))()
