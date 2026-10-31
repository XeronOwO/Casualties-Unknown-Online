// recipe: bandage-ring-state
// args: none
// returns: ok, frames, moves, actions, running, kind, error, detail
//
// Reads the counters the `bandage-ring` declaration keeps: how many frames it ran, how many pointer moves it staged, how many of the
// game's own actions it saw, whether it is still running, and what it last saw (the minigame kind, or why it could not start).
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type ring = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { ring = assemblies[i].GetType("AcceptanceBandageRing", false); } catch { ring = null; }
		if (ring != null) { break; }
	}
	if (ring == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare bandage-ring first\"}"; }

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

	return "{\"ok\":true,\"frames\":" + num(ring, "Frames").ToString(inv)
		+ ",\"moves\":" + num(ring, "Moves").ToString(inv)
		+ ",\"actions\":" + num(ring, "Actions").ToString(inv)
		+ ",\"running\":" + (num(ring, "Running") > 0 ? "true" : "false")
		+ ",\"kind\":\"" + text(ring, "Kind") + "\"}";
}))()
