// recipe: geyser-start
// args: none
// serves: save-continue — force the own Start of every geyser that has not run it
// returns: ok, started, error
//
// Calls the native GeyserScript.Start on every geyser whose own Start has not run (bld == null).
// The Harmony postfix marks it started and re-applies the restored type; the caller then re-reads
// geyser-types to observe the type after Start. One eval on the host.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var start = typeof(GeyserScript).GetMethod("Start", flags);
	if (start == null) { return "{\"ok\":false,\"error\":\"Start not found\"}"; }
	var all = UnityEngine.Object.FindObjectsOfType<GeyserScript>(true);
	var started = 0;
	for (var i = 0; i < all.Length; i++) {
		var g = all[i];
		if (g == null) { continue; }
		var bldField = g.GetType().GetField("bld", flags);
		if (bldField != null && bldField.GetValue(g) != null) { continue; }
		start.Invoke(g, null);
		started++;
	}
	return "{\"ok\":true,\"started\":" + started.ToString(inv) + "}";
}))()
