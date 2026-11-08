// recipe: pinyin-probe
// args: none
// serves: pinyin-search-standalone-mod (the machine attempt at the native search box: is the matcher, the switch and the
// reading table live inside the running client?)
// returns: ok, enabled, containsCui, containsCuiruo, containsFull, tableChars, error, detail
//
// Reads the pinyin mod's own live state by reflection: the switch the patch reads (`PinyinSearchGate.Enabled`), the
// matcher's answer for the fragile knife's Chinese name against `cui` / `cuiruo` / the full reading, and the reading
// table's character count when the type exposes one. Reflection only, never a cast.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var find = new System.Func<string, System.Type>((simpleName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } }
		}
		return null;
	});
	var gate = find("PinyinSearchGate");
	var enabled = false;
	if (gate != null) {
		var property = gate.GetProperty("Enabled", F);
		enabled = property != null && (bool)property.GetValue(null, null);
	}
	var matcher = find("NameSearchMatcher");
	if (matcher == null) { return "{\"ok\":false,\"error\":\"no-matcher\"}"; }
	var matches = matcher.GetMethod("Matches", F);
	var name = "\u8106\u5f31\u7684\u5200";
	var containsCui = (bool)matches.Invoke(null, new object[] { name, "cui" });
	var containsCuiruo = (bool)matches.Invoke(null, new object[] { name, "cuiruo" });
	var containsFull = (bool)matches.Invoke(null, new object[] { name, "cuiruodedao" });
	var tableChars = 0;
	var dictionary = find("PinyinDictionary");
	if (dictionary != null) {
		var fields = dictionary.GetFields(F);
		for (var i = 0; i < fields.Length; i++) {
			var value = fields[i].GetValue(null);
			if (value is string text) { tableChars += text.Length; }
			if (value is System.Collections.IDictionary map) { tableChars += map.Count; }
		}
	}
	return "{\"ok\":true,\"enabled\":" + (enabled ? "true" : "false") + ",\"containsCui\":" + (containsCui ? "true" : "false") + ",\"containsCuiruo\":" + (containsCuiruo ? "true" : "false") + ",\"containsFull\":" + (containsFull ? "true" : "false") + ",\"tableChars\":" + tableChars + "}";
}))()
