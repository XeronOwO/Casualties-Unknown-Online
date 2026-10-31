// recipe: audio-watch3-read
// args: none
// returns: ok, frames, plays, running, names, error, detail
//
// Reads what the `audio-watch3` declaration collected: how many frames it sampled, how many PLAYBACKS it counted (one per distinct
// AudioSource that played the matched clip, which is how many times the game played it), whether it is still running, and the distinct
// clip names it saw.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type watch = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { watch = assemblies[i].GetType("AcceptanceAudioWatch3", false); } catch { watch = null; }
		if (watch != null) { break; }
	}
	if (watch == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare audio-watch3 first\"}"; }

	var num = new System.Func<System.Type, string, int>((type, name) => {
		var info = type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		if (info == null) { return -1; }
		var value = info.GetValue(null);
		return value is bool ? (((bool)value) ? 1 : 0) : System.Convert.ToInt32(value);
	});
	var text = new System.Func<System.Type, string, string>((type, name) => {
		var info = type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		if (info == null) { return ""; }
		var value = info.GetValue(null);
		return value == null ? "" : value.ToString();
	});

	return "{\"ok\":true,\"frames\":" + num(watch, "Frames").ToString(inv)
		+ ",\"plays\":" + num(watch, "Plays").ToString(inv)
		+ ",\"running\":" + (num(watch, "Running") > 0 ? "true" : "false")
		+ ",\"names\":\"" + text(watch, "Names") + "\"}";
}))()
