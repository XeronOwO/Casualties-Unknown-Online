// recipe: game-console-focus
// args: none
// serves: enter-key-opens-plain-chat
// returns: ok, guardFound, gameConsoleActive, selected, selectedActiveInHierarchy, selectedHasTmpInput,
//          selectedHasLegacyInput, guard
//
// Reads the GAME's keyboard-ownership facts the CUO Enter/Slash opener is gated on: whether the game's own
// console is open (`ConsoleScript.active`), what the game's event system currently has selected and whether
// that object is active in hierarchy, whether it carries a real text field (both component kinds), and what
// the adapter's guard (`OnlineUiKeyboardFacts.IsTheGameUsingTheKeyboard`) answers. It exists because the
// opener is refused exactly while that guard is true, and the guard is internal to the adapter: reading it
// through reflection is how a run shows the bug instead of inferring it. A guard that cannot be found is an
// ERROR, never a false green. No input is posted here; pair it with `key-hold` (the game's own `console`
// bind) to open and close the console and `key-tap` (a raw virtual key) for the Return the opener watches.
((System.Func<string>)(() => {
	var console = ConsoleScript.instance;
	var active = console != null && console.active;
	var selected = UnityEngine.EventSystems.EventSystem.current == null ? null : UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
	var selectedName = selected == null ? "" : selected.name;
	var selectedActive = selected != null && selected.activeInHierarchy;
	var hasTmp = selected != null && selected.GetComponent<TMPro.TMP_InputField>() != null;
	var hasLegacy = selected != null && selected.GetComponent<UnityEngine.UI.InputField>() != null;
	var guardType = (System.Type)null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && guardType == null; i++) {
		guardType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.OnlineUiKeyboardFacts", false);
	}
	var guardFound = guardType != null;
	var guard = false;
	if (guardFound) {
		var method = guardType.GetMethod("IsTheGameUsingTheKeyboard", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
		guardFound = method != null;
		if (guardFound) { guard = (bool)method.Invoke(null, null); }
	}
	var escape = (System.Func<string, string>)(text => text == null ? "" : text.Replace("\\", "/").Replace("\"", "'").Replace("\r", " ").Replace("\n", " / "));
	if (!guardFound) {
		return "{\"ok\":false,\"error\":\"guard-not-found\",\"detail\":\"the adapter's OnlineUiKeyboardFacts.IsTheGameUsingTheKeyboard was not found in this client\"}";
	}
	return "{\"ok\":true,\"guardFound\":true"
		+ ",\"gameConsoleActive\":" + (active ? "true" : "false")
		+ ",\"selected\":\"" + escape(selectedName) + "\""
		+ ",\"selectedActiveInHierarchy\":" + (selectedActive ? "true" : "false")
		+ ",\"selectedHasTmpInput\":" + (hasTmp ? "true" : "false")
		+ ",\"selectedHasLegacyInput\":" + (hasLegacy ? "true" : "false")
		+ ",\"guard\":" + (guard ? "true" : "false") + "}";
}))()
