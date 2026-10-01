// recipe: pause-toggle
// args: none
// serves: world-acceleration-survives-movement, world-time-local-initiation
// returns: ok, paused, timeScale, curTimeScale
//
// Toggles the game's own pause state: the call the ESC handler makes (PlayerCamera.cs:876-879 ->
// PauseHandler.TogglePause, PauseHandler.cs:31). The pause container's own Update ramps pauseProgress and
// then writes the clock through PlayerCamera.SetTimeScale(Paused, false, true) — the forced transition
// the world-time rule routes as local-only (PauseHandler.cs:153-160). The write lands a frame or more
// later, so read the state back with time-scale-read instead of trusting this answer.
((System.Func<string>)(() => {
	PauseHandler.TogglePause();
	return "{\"ok\":true"
		+ ",\"paused\":" + (PauseHandler.paused ? "true" : "false")
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"curTimeScale\":\"" + (PlayerCamera.main != null ? PlayerCamera.main.curTimeScale.ToString() : "none") + "\"}";
}))()
