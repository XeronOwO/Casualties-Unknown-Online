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
	// What the seam holds for the pause bind right now: a pressed key the game never acts on has to be separated from a
	// press the seam already dropped (measured 2026-11-23: every pause gate open, the clock still ran).
	var bindDown = "?";
	var bindHeld = "?";
	var bindName = "?";
	try
	{
		var pauseBind = KeyBinds.GetBind("pause");
		bindName = pauseBind.ToString();
		var findType = new System.Func<string, System.Type>((simpleName) => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { System.Type[] types; try { types = assemblies[i].GetTypes(); } catch { continue; } for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } } } return null; });
	var seam = findType("VirtualInput");
		var query = findType("ButtonQuery");
		var ask = seam == null || query == null ? null : seam.GetMethod("TryAnswerKey", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		var answer = new System.Func<string, string>((name) => {
			if (ask == null) { return "?"; }
			var slot = new object[] { pauseBind, System.Enum.Parse(query, name), null };
			var held = ask.Invoke(null, slot);
			return (held is bool matched && matched ? "1" : "0") + ((slot[2] is bool value && value) ? "1" : "0");
		});
		bindDown = answer("Down");
		bindHeld = answer("Held");
	}
	catch { }
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
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"bind\":\"" + bindName + "\""
		+ ",\"bindDown\":\"" + bindDown + "\""
		+ ",\"bindHeld\":\"" + bindHeld + "\"}";
}))()
