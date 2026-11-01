// recipe: lockpress3
// args: x=n y=n frames=n
// returns: ok, started, frames, presses, error, detail
//
// Starts the `lockpress3` declaration: it parks the staged pointer on a point of the lockpick ring and pumps press/release that many frames, so at least one
// `GetKeyDown(attack)` edge lands while the hand is inside the ring band — which is the only frame `LockpingMinigame.clickingInside` is written on, and the
// stuck branch (the one that plays `gore2`) lives inside it. Returns at once; the verdict is the minigame's own `clickingInside`/`lockProgress` and the
// acting client's report record (`report-watch-read`).
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var frames = (int)({{n:frames}});
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type press = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { press = assemblies[i].GetType("AcceptanceLockPress3", false); } catch { press = null; }
		if (press != null) { break; }
	}
	if (press == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare lockpress3 first\"}"; }
	var start = press.GetMethod("Start");
	if (start == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var started = (string)start.Invoke(null, new object[] { x, y, frames });
	return "{\"ok\":true,\"started\":\"" + started + "\",\"frames\":" + frames.ToString(inv) + ",\"presses\":0}";
}))()
