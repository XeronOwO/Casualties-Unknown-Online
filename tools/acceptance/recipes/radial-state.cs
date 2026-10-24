// recipe: radial-state
// args: none
// serves: cross-player-drag-use-model-bounds (why the ring did not open, and whether the game was even listening)
// returns: ok, radialOpen, paused, focused, timeScale, bind, buttons, error, detail
//
// The bag in this game IS the radial ring (`PlayerCamera.Update`: `toggleinventory` flips `radialOpen`, gated by
// `!PauseHandler.paused`), so "the bag is closed" has exactly three candidate causes and this probe separates them:
// the ring flag itself, the pause gate the toggle reads, and whether the game window is focused at all. It also reports the
// bind the settings hold, so a batch never presses a guessed key.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var buttons = UnityEngine.Object.FindObjectsOfType<InvButton>();
	var active = 0;
	for (var i = 0; i < buttons.Length; i++) { if (buttons[i] != null && buttons[i].gameObject.activeInHierarchy) { active++; } }
	return "{\"ok\":true"
		+ ",\"radialOpen\":" + (camera.radialOpen ? "true" : "false")
		+ ",\"paused\":" + (PauseHandler.paused ? "true" : "false")
		+ ",\"focused\":" + (UnityEngine.Application.isFocused ? "true" : "false")
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", inv)
		+ ",\"bind\":\"" + KeyBinds.GetBind("toggleinventory").ToString() + "\""
		+ ",\"buttons\":" + active.ToString(inv) + "}";
}))()
