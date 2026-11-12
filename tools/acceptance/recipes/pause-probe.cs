// recipe: pause-probe
// args: none
// returns: ok, before, after, pausedProperty, exception, timeScale
//
// Calls the game's own `PauseHandler.TogglePause` and reads the handler's own field in the same eval, with
// the exception text kept: a toggle that refuses without an exception is a different finding from one
// that throws inside the sound call.
((System.Func<string>)(() => {
	var main = PauseHandler.main;
	var before = main != null && main.isPaused;
	var exception = "";
	try
	{
		PauseHandler.TogglePause();
	}
	catch (System.Exception e)
	{
		exception = e.GetType().Name + ": " + e.Message;
	}
	var after = main != null && main.isPaused;
	return "{\"ok\":true"
		+ ",\"before\":" + (before ? "true" : "false")
		+ ",\"after\":" + (after ? "true" : "false")
		+ ",\"pausedProperty\":" + (PauseHandler.paused ? "true" : "false")
		+ ",\"exception\":\"" + exception.Replace("\\", "/").Replace("\"", "'") + "\""
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ "}";
}))()
