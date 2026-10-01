// recipe: game-console
// args: command=s args=s
// serves: save-layer-end-save-and-restore, save-mid-run-consistent-cut, save-native-run-field-parity,
//         save-native-character-field-parity, save-run-clock-not-sent
// returns: ok, command, args, applied, error, detail
//
// Runs one of the game's own registered debug console commands (spawn, explode, floodfill, locate,
// skiplayer, kill, heal, tp, ...) in-process: the very delegate the native console executes, reached
// without OS-level input. `args` is the command's own argument line with single spaces between
// arguments; the literal `none` means no arguments (for commands like kill/heal). A position argument
// uses the game's own comma-free anchors (`cursor`, `player`, `random`) because the driver's
// -RecipeArg values may not contain commas. The delegate's own output goes to the native console, not
// to this answer.
((System.Func<string>)(() => {
	var name = {{s:command}};
	var line = {{s:args}};
	var found = ConsoleScript.SearchExact(name);
	if (found == null) { return "{\"ok\":false,\"error\":\"unknown-game-command\",\"detail\":\"no registered command " + name + "\"}"; }
	var parts = (line.Length == 0 || line == "none") ? new string[0] : line.Split(' ');
	var call = new string[parts.Length + 1];
	call[0] = name;
	for (var i = 0; i < parts.Length; i++) { call[i + 1] = parts[i]; }
	found.action(call);
	return "{\"ok\":true,\"command\":\"" + name + "\",\"args\":" + parts.Length.ToString() + ",\"applied\":true}";
}))()
