// recipe: pause-state
// args: none
// returns: ok, paused, minigame, woundView, crafting, customizer, trade, radial, scrollableText, timeScale
//
// The game's own pause key runs a chain of branches before it pauses: an open minigame, wound view, craft
// panel, item customizer, trade menu, radial or a scrollable text each take the key first (PlayerCamera.cs:986-1020).
// A row that pressed the pause binding and saw the clock keep running needs to know which one of them took it,
// otherwise "the key never arrived" and "the game used the key for a panel" read identically.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var main = PlayerCamera.main;
	if (main == null) { return "{\"ok\":false,\"error\":\"no-player-camera\"}"; }
	var boolOf = new System.Func<object, bool>((value) => value is bool flag && flag);
	var flagOf = new System.Func<System.Func<object>, bool>((read) => { try { return boolOf(read()); } catch { return false; } });
	var field = new System.Func<string, object>((name) => { var info = typeof(PlayerCamera).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); return info == null ? null : info.GetValue(main); });
	var scrollable = false;
	try { scrollable = ScrollableText.textExists; } catch { }
	return "{\"ok\":true"
		+ ",\"paused\":" + (PauseHandler.paused ? "true" : "false")
		+ ",\"minigame\":" + ((MinigameBase.main != null && MinigameBase.main.currentMinigame != null) ? "true" : "false")
		+ ",\"woundView\":" + (flagOf(() => field("woundView") is UnityEngine.GameObject wound && wound.activeSelf) ? "true" : "false")
		+ ",\"crafting\":" + (flagOf(() => field("craftingPanel") is UnityEngine.GameObject craft && craft.activeSelf) ? "true" : "false")
		+ ",\"customizer\":" + (flagOf(() => field("itemCustomizer") is UnityEngine.GameObject custom && custom.activeSelf) ? "true" : "false")
		+ ",\"trade\":" + (flagOf(() => field("tradeMenu") is UnityEngine.GameObject trade && trade.activeSelf) ? "true" : "false")
		+ ",\"radial\":" + (flagOf(() => field("radialOpen")) ? "true" : "false")
		+ ",\"scrollableText\":" + (scrollable ? "true" : "false")
		+ ",\"handler\":" + (PauseHandler.main != null ? "true" : "false")
		+ ",\"settings\":" + (SettingsMenu.instance != null ? "true" : "false")
		+ ",\"deathScreen\":" + (flagOf(() => field("didDeathScreen")) ? "true" : "false")
		+ ",\"selfDestruct\":" + (flagOf(() => field("selfDestructActive")) ? "true" : "false")
		+ ",\"generating\":" + (flagOf(() => WorldGeneration.world != null && WorldGeneration.world.generatingWorld) ? "true" : "false")
		+ ",\"cutscene\":" + (flagOf(() => WorldGeneration.world != null && WorldGeneration.world.cutsceneActive) ? "true" : "false")
		+ ",\"disallowed\":" + (flagOf(() => PauseHandler.main != null && PauseHandler.main.disallowPause) ? "true" : "false")
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
