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
	// The toggle branch sits behind three early returns in PlayerCamera.Update: an open crafting panel, an open trade menu,
	// and any active minigame all stop the frame before the inventory key is read. A "the key does nothing" reading that
	// does not report these three is not a reading of the key at all.
	var crafting = false;
	var trade = false;
	var minigame = "none";
	var craftingPanel = typeof(PlayerCamera).GetField("craftingPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	if (craftingPanel != null) {
		var panel = craftingPanel.GetValue(camera) as UnityEngine.GameObject;
		crafting = panel != null && panel.activeSelf;
	}
	var tradeMenu = typeof(PlayerCamera).GetField("tradeMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	if (tradeMenu != null) {
		var menu = tradeMenu.GetValue(camera) as UnityEngine.GameObject;
		trade = menu != null && menu.activeSelf;
	}
	if (MinigameBase.main != null && MinigameBase.main.currentMinigame != null) { minigame = MinigameBase.main.currentMinigame.GetType().Name; }
	var cameras = UnityEngine.Object.FindObjectsOfType<PlayerCamera>();
	var live = "none";
	for (var i = 0; i < cameras.Length; i++) {
		var candidate = cameras[i];
		if (candidate == null) { continue; }
		if (candidate == camera) { live = "main"; }
	}
	var consoles = UnityEngine.Object.FindObjectsOfType<ConsoleScript>();
	var consoleActive = false;
	for (var i = 0; i < consoles.Length; i++) { if (consoles[i] != null && consoles[i].active) { consoleActive = true; } }
	return "{\"ok\":true"
		+ ",\"consoleCount\":" + consoles.Length.ToString(inv)
		+ ",\"consoleActive\":" + (consoleActive ? "true" : "false")
		+ ",\"cameras\":" + cameras.Length.ToString(inv)
		+ ",\"mainIsLive\":\"" + live + "\""
		+ ",\"enabled\":" + (camera.enabled ? "true" : "false")
		+ ",\"frame\":" + UnityEngine.Time.frameCount.ToString(inv)
		+ ",\"radialOpen\":" + (camera.radialOpen ? "true" : "false")
		+ ",\"paused\":" + (PauseHandler.paused ? "true" : "false")
		+ ",\"focused\":" + (UnityEngine.Application.isFocused ? "true" : "false")
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", inv)
		+ ",\"bind\":\"" + KeyBinds.GetBind("toggleinventory").ToString() + "\""
		+ ",\"craftingPanel\":" + (crafting ? "true" : "false")
		+ ",\"tradeMenu\":" + (trade ? "true" : "false")
		+ ",\"minigame\":\"" + minigame + "\""
		+ ",\"buttons\":" + active.ToString(inv) + "}";
}))()
