// recipe: shrapnel-break
// args: count=n
// returns: ok, started, count, pieces, error, detail
//
// Starts the `shrapnel-break` declaration's coroutine, which grabs, reports and breaks shrapnel pieces across SUCCESSIVE FRAMES
// inside the shared session's lifetime. The recipe returns at once — the pieces fall while the game keeps running — and the batch
// reads the result with `pieces` and the limb read afterwards, never from here.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var count = (int)({{n:count}});
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type declaration = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (declaration == null) { try { declaration = assemblies[i].GetType("AcceptanceShrapnelBreak", false); } catch { declaration = null; } }
	}
	if (declaration == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare shrapnel-break first\"}"; }
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
	return "{\"ok\":true,\"started\":\"" + started + "\""
		+ ",\"count\":" + count.ToString(inv)
		+ ",\"pieces\":\"" + pieces + "\"}";
}))()
