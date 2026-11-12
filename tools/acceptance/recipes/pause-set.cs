// recipe: pause-set
// args: value=s
// returns: ok, value, isPaused, timeScale, error
//
// Writes the pause handler's own `isPaused` field (a fixture write, the same field the ESC handler toggles)
// so a row can separate the container's own clock ramp from whatever refuses `TogglePause`. Read the clock
// back with `time-scale-read` after a frame; `value=off` restores play.
((System.Func<string>)(() => {
	var value = {{s:value}};
	var main = PauseHandler.main;
	if (main == null) { return "{\"ok\":false,\"error\":\"no-pause-handler\"}"; }
	main.isPaused = value == "on";
	return "{\"ok\":true"
		+ ",\"value\":\"" + value + "\""
		+ ",\"isPaused\":" + (main.isPaused ? "true" : "false")
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ "}";
}))()
