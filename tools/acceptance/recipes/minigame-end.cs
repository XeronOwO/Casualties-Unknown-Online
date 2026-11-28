// recipe: minigame-end
// args: (none)
// serves: 1.0-one-acceptance-run-covers-every-case (pause-clock)
// returns: ok, had
//
// Ends the minigame the game still has open, if any. The bandage wrap's minigame lives in the game,
// not in the acceptance hand drive: `bandage-hand stop` stops the drive, and `MinigameBase.main.
// currentMinigame` can stay set across rows (2026-11-28 full-56 read `minigame:true` before pause-clock),
// which makes the game refuse a pause (`PauseHandler.disallowPause`). `MinigameBase.EndMinigame`
// (`MinigameBase.cs:266`) is the game's own close path.
((System.Func<string>)(() => {
	var findType = new System.Func<string, System.Type>((simpleName) => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { System.Type[] types; try { types = assemblies[i].GetTypes(); } catch { continue; } for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } } } return null; });
	var type = findType("MinigameBase");
	if (type == null) { return "{\"ok\":false,\"error\":\"MinigameBase is not in this build\"}"; }
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mainField = type.GetField("main", Flags);
	if (mainField == null) { return "{\"ok\":false,\"error\":\"MinigameBase.main is not a field\"}"; }
	var main = mainField.GetValue(null);
	if (main == null) { return "{\"ok\":false,\"error\":\"MinigameBase.main is null\"}"; }
	const System.Reflection.BindingFlags InstanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var currentField = type.GetField("currentMinigame", InstanceFlags);
	var current = currentField == null ? null : currentField.GetValue(main);
	if (current == null) { return "{\"ok\":true,\"had\":false}"; }
	var end = type.GetMethod("EndMinigame", InstanceFlags);
	if (end == null) { return "{\"ok\":false,\"error\":\"MinigameBase.EndMinigame is not a method\"}"; }
	end.Invoke(main, new object[0]);
	return "{\"ok\":true,\"had\":true}";
}))()
