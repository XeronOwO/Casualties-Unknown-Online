// recipe: input-probe
// args: none
// serves: cross-player-drag-use-model-bounds (whether an injected key reaches the game's own Input at all)
// returns: ok, focused, heldKey, keyDown, inputString, error, detail
//
// Separates the two ways a key press can fail to arrive: Unity's Input never sees the key (an injection problem), or it
// sees it and the game's own gate refuses (a game-state problem). Read WHILE a helper holds the key down, it answers the
// first question directly - `heldKey` is `Input.GetKey` for the very bind the game reads, and `keyDown` is the frame-edge
// form. The bind comes from the game's own cache, so the probe cannot disagree with the game about which key it means.
((System.Func<string>)(() => {
	var bind = KeyBinds.GetBind("toggleinventory");
	var held = UnityEngine.Input.GetKey(bind);
	var down = UnityEngine.Input.GetKeyDown(bind);
	return "{\"ok\":true"
		+ ",\"focused\":" + (UnityEngine.Application.isFocused ? "true" : "false")
		+ ",\"bind\":\"" + bind.ToString() + "\""
		+ ",\"heldKey\":" + (held ? "true" : "false")
		+ ",\"keyDown\":" + (down ? "true" : "false")
		+ ",\"inputString\":\"" + UnityEngine.Input.inputString.Replace("\"", "'") + "\"}";
}))()
