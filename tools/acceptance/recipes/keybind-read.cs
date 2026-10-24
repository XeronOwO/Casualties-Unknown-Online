// recipe: keybind-read
// args: action=s
// serves: cross-player-drag-use-model-bounds (opening the bag the drag starts from, without guessing the bind)
// returns: ok, action, keyCode, keyName, error, detail
//
// Answers what the game's OWN key-bind cache holds for a named action, so a batch presses the key the player's settings
// actually bound instead of a guessed virtual-key code. The value is the game's: `KeyBinds.GetBind` reads the cache the
// settings UI fills, and `KeyCode`'s integer is the value the in-process key bridge needs.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var action = {{s:action}};
	var code = KeyBinds.GetBind(action);
	return "{\"ok\":true,\"action\":\"" + action + "\",\"keyCode\":" + ((int)code).ToString(inv)
		+ ",\"keyName\":\"" + code.ToString() + "\"}";
}))()
