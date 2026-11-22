// recipe: console-game
// args: none
// returns: ok, commands, names, spawnAutofill
//
// The game's own console command table, read from `ConsoleScript.Commands` — the list the game itself searches when a word
// is entered (`ConsoleScript.SearchExact`, `TryExecuteCommand`). A row about our console's completion has to compare what
// our console offers with THIS list; a name we invented is a defect, a name the game ships that we hide is a gap. Each
// `Command` carries `name` (a public field) and `argAutofill` (a dictionary of argument positions to candidate words), so a
// name read as empty means the field lookup is wrong, not that the game ships nameless commands.
//
// Read-only.
((System.Func<string>)(() => { var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance; var consoleType = System.Type.GetType("ConsoleScript, Assembly-CSharp"); if (consoleType == null) { return "{\"ok\":false,\"error\":\"no ConsoleScript type\"}"; } var commandsField = consoleType.GetField("Commands", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); var list = commandsField == null ? null : commandsField.GetValue(null) as System.Collections.IEnumerable; if (list == null) { return "{\"ok\":false,\"error\":\"no Commands list\"}"; } var names = ""; var count = 0; var spawnAutofill = 0; foreach (var command in list) { if (command == null) { continue; } count++; var type = command.GetType(); var nameField = type.GetField("name", flags); var name = nameField == null ? "" : (nameField.GetValue(command) as string) ?? ""; names = names + name + ";"; if (name == "spawn") { var autofillField = type.GetField("argAutofill", flags); var autofill = autofillField == null ? null : autofillField.GetValue(command) as System.Collections.IDictionary; spawnAutofill = autofill == null ? 0 : autofill.Count; } } return "{\"ok\":true,\"commands\":" + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"spawnAutofill\":" + spawnAutofill.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"names\":\"" + names.Replace("\\", "/").Replace("\"", "''") + "\"}"; }))()
