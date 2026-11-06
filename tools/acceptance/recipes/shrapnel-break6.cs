// recipe: shrapnel-break6
// args: count=n
// returns: ok, started, count, pieces, steps, trace, error, detail
//
// Starts the `shrapnel-break6` declaration's coroutine and reports its opening reading. The coroutine grabs each piece,
// reports the grab (the host applies the damage), breaks it, lifts it above the game's y=35 line, and then sends the
// ordinary position report whose Y the host reads as "this piece left the limb" — which is what makes
// `AllPiecesRemoved` terminate the shared session `Completed`. The batch then reads the host's `[ShrapnelBreak]` and
// `[Shrapnel] session … removed` lines and the session's terminal.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var count = (int)({{n:count}});
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type declaration = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (declaration == null) { try { declaration = assemblies[i].GetType("AcceptanceShrapnelBreak6", false); } catch { declaration = null; } }
	}
	if (declaration == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare shrapnel-break6 first\"}"; }
	var start = declaration.GetMethod("Start");
	if (start == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var started = (string)start.Invoke(null, new object[] { count });
	var pieces = "none";
	var minigameBase = MinigameBase.main;
	if (minigameBase != null && minigameBase.currentMinigame is ShrapnelMinigame) {
		var field = typeof(ShrapnelMinigame).GetField("objects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		if (field != null) {
			var list = field.GetValue(minigameBase.currentMinigame) as System.Collections.Generic.List<UnityEngine.RectTransform>;
			if (list != null) { pieces = list.Count.ToString(inv); }
		}
	}
	var trace = "none";
	var traceField = declaration.GetField("Trace");
	if (traceField != null) { trace = (string)traceField.GetValue(null) ?? "null"; }
	var stepCount = "0";
	var stepsField = declaration.GetField("Steps");
	if (stepsField != null) {
		var steps = stepsField.GetValue(null) as System.Collections.Generic.List<string>;
		if (steps != null) { stepCount = steps.Count.ToString(inv); }
	}
	return "{\"ok\":true,\"started\":\"" + started + "\""
		+ ",\"count\":" + count.ToString(inv)
		+ ",\"pieces\":\"" + pieces + "\""
		+ ",\"steps\":" + stepCount
		+ ",\"trace\":\"" + trace.Replace("\"", "'") + "\"}";
}))()
