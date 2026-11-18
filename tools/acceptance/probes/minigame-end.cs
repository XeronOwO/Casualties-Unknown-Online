// recipe: minigame-end
// args: none
// serves: operational recovery - a staged body can be left inside a native minigame (a self-harm episode from a zeroed
//         happiness is the one this batch produced), and an active minigame blocks the game's own drag bracket
//         (`HandleDragging` requires `!MinigameBase.main.currentMinigame`), so it must be ended deliberately
// returns: ok, before, canExit, ended, after, error, detail
//
// Reads the active minigame through the game's own `MinigameBase`, asks its own `CanExit` gate, and calls its own
// `EndMinigame()` - the same pair the pause key's branch calls. Read-only if there is no minigame.
((System.Func<string>)(() => {
	var baseMain = MinigameBase.main;
	if (baseMain == null) { return "{\"ok\":false,\"error\":\"no-minigame-base\"}"; }
	var before = baseMain.currentMinigame == null ? "none" : baseMain.currentMinigame.GetType().Name;
	var canExit = baseMain.currentMinigame != null && baseMain.currentMinigame.CanExit();
	var ended = false;
	if (baseMain.currentMinigame != null && canExit) {
		baseMain.EndMinigame();
		ended = true;
	}
	var after = baseMain.currentMinigame == null ? "none" : baseMain.currentMinigame.GetType().Name;
	return "{\"ok\":true,\"before\":\"" + before + "\""
		+ ",\"canExit\":" + (canExit ? "true" : "false")
		+ ",\"ended\":" + (ended ? "true" : "false")
		+ ",\"after\":\"" + after + "\"}";
}))()
