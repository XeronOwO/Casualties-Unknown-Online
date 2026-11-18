// recipe: cpr-rhythm
// args: count=n interval=n control=s
// serves: remote-medical-cpr (the `Good` verdict row, and any row that needs presses the game's own clock spaces)
// returns: ok, started, count, interval, control, error, detail
//
// Starts the `cpr-rhythm` declaration's coroutine, which presses the CPR control once per interval ON SUCCESSIVE FRAMES.
// The recipe itself returns at once: the presses happen while the game keeps running, so the control's own real-time gap
// measurement sees the spacing the batch asked for. The verdicts are then read from the operator's log, never from here.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var count = (int)({{n:count}});
	var interval = {{n:interval}};
	var control = {{s:control}};
	var type = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type rhythm = null;
	for (var i = 0; i < type.Length; i++) {
		try { rhythm = type[i].GetType("AcceptanceCprRhythm", false); } catch { rhythm = null; }
		if (rhythm != null) { break; }
	}
	if (rhythm == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare cpr-rhythm first\"}"; }
	var method = rhythm.GetMethod("Start");
	if (method == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var started = (string)method.Invoke(null, new object[] { count, (float)interval, control });
	return "{\"ok\":true,\"started\":\"" + started + "\""
		+ ",\"count\":" + count.ToString(inv)
		+ ",\"interval\":" + interval.ToString("0.###", inv)
		+ ",\"control\":\"" + control + "\"}";
}))()
