// recipe: pause-state
// args: none
// returns: ok, hasMain, isPaused, disallowPause, generating, deathScreen, selfDestruct, settingsMenu, timeScale, error
//
// Names the game's own pause gate the way `PauseHandler` reads it: the handler must exist, its settings
// menu must not, and `disallowPause` (death screen, self-destruct, world still generating) must be false.
// The ESC handler's own call is `pause-toggle`; this recipe only reads which of the three held, so a
// refused toggle can be judged instead of guessed.
((System.Func<string>)(() => {
	var main = PauseHandler.main;
	var camera = PlayerCamera.main;
	var world = WorldGeneration.world;
	var generating = world != null && world.generatingWorld;
	var settings = SettingsMenu.instance != null;
	var death = camera != null && camera.didDeathScreen;
	var selfDestruct = camera != null && camera.selfDestructActive;
	return "{\"ok\":true"
		+ ",\"hasMain\":" + (main != null ? "true" : "false")
		+ ",\"isPaused\":" + (main != null && main.isPaused ? "true" : "false")
		+ ",\"disallowPause\":" + (main != null && main.disallowPause ? "true" : "false")
		+ ",\"generating\":" + (generating ? "true" : "false")
		+ ",\"deathScreen\":" + (death ? "true" : "false")
		+ ",\"selfDestruct\":" + (selfDestruct ? "true" : "false")
		+ ",\"settingsMenu\":" + (settings ? "true" : "false")
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ "}";
}))()
