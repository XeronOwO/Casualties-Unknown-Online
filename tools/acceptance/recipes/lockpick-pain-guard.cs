// recipe: lockpick-pain-guard
// args: x=n y=n baseline=n timeout=n
// serves: lockpick row (the hand's first pain ends the picking)
// returns: ok, started, error
//
// Presses the attack key through `AcceptanceLockpickPainGuard` and returns at once: the guard's own coroutine watches
// the body on every frame and releases the key the frame the pain rises above `baseline`. The guard presses instead of
// this recipe because a round trip is several hundred milliseconds long, and the body pays 20 pain every half second
// while the pick is stuck.
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
	var started = guard.GetMethod("Start", Flags).Invoke(null, new object[] {
		(float){{n:x}}, (float){{n:y}}, (float){{n:baseline}}, (float){{n:timeout}} });
	if (!string.Equals(started as string, "started", System.StringComparison.Ordinal)) {
		return "{\"ok\":false,\"error\":\"" + (started as string) + "\"}";
	}
	return "{\"ok\":true,\"started\":\"started\"}";
}))()
