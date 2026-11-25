// recipe: lockpick-pain-guard-state
// args: none
// serves: lockpick row (the hand's first pain ends the picking)
// returns: ok, trace, running, pain, baseline, progress, hasMain, kind, averagePain
//
// The guard's own record: whether it still holds the key, the trace it will end on, and the pain it last read. The row
// waits on `running` turning false (an event the game itself produced), never on a fixed pause.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	System.Type guard = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && guard == null; i++) {
		System.Type[] types;
		try { types = assemblies[i].GetTypes(); } catch { continue; }
		for (var t = 0; t < types.Length; t++) { if (types[t].Name == "AcceptanceLockpickPainGuard") { guard = types[t]; break; } }
	}
	if (guard == null) { return "{\"ok\":false,\"error\":\"no-guard\"}"; }
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var trace = (string)guard.GetField("Trace", Flags).GetValue(null);
	var running = (bool)guard.GetField("Running", Flags).GetValue(null);
	var pain = System.Convert.ToSingle(guard.GetField("Pain", Flags).GetValue(null));
	var baseline = System.Convert.ToSingle(guard.GetField("Baseline", Flags).GetValue(null));
	var progress = System.Convert.ToSingle(guard.GetMethod("Progress", Flags).Invoke(null, new object[0]));
	var main = MinigameBase.main;
	var current = main == null ? null : main.currentMinigame;
	return "{\"ok\":true,\"trace\":\"" + (trace ?? "") + "\""
		+ ",\"running\":" + (running ? "true" : "false")
		+ ",\"pain\":" + pain.ToString("0.###", inv)
		+ ",\"baseline\":" + baseline.ToString("0.###", inv)
		+ ",\"progress\":" + progress.ToString("0.###", inv)
		+ ",\"hasMain\":" + (main != null ? "true" : "false")
		+ ",\"kind\":\"" + (current == null ? "none" : current.GetType().Name) + "\""
		+ ",\"averagePain\":" + (main != null && main.body != null ? main.body.averagePain.ToString("0.###", inv) : "null") + "}";
}))()
