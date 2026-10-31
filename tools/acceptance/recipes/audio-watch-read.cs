// recipe: audio-watch-read
// args: none
// returns: ok, frames, seen, running, hits, error, detail
//
// Reads the counters and the clip names the `audio-watch` declaration collected: how many frames it sampled, how many distinct clips
// it saw playing that matched, whether it is still running, and the names themselves joined by `|`.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type watch = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { watch = assemblies[i].GetType("AcceptanceAudioWatch2", false); } catch { watch = null; }
		if (watch != null) { break; }
	}
	if (watch == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare audio-watch first\"}"; }

	var num = new System.Func<System.Type, string, int>((type, name) => {
		var info = type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		if (info == null) { return -1; }
		var value = info.GetValue(null);
		return value is bool ? (((bool)value) ? 1 : 0) : System.Convert.ToInt32(value);
	});

	var hitsField = watch.GetField("Hits", flags);
	var hits = hitsField == null ? "" : (string)hitsField.GetValue(null);

	return "{\"ok\":true,\"frames\":" + num(watch, "Frames").ToString(inv)
		+ ",\"seen\":" + num(watch, "Seen").ToString(inv)
		+ ",\"running\":" + (num(watch, "Running") > 0 ? "true" : "false")
		+ ",\"hits\":\"" + hits + "\"}";
}))()
