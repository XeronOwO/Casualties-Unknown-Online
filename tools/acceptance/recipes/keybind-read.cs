// recipe: keybind-read
// args: action=s
// returns: ok, action, bind, error, detail
//
// Reads the CURRENT binding through the game's own table (`KeyBinds.GetBindName`, KeyBinds.cs: cache of the settings' bindings).
// Needed because the drag gesture is not a mouse button: `HandleDragging` (PlayerCamera.cs:1354-1363) reads
// `Input.GetKeyDown/GetKeyUp(KeyBinds.GetBind("iteminteract"))`, so a staged drag has to press THAT key.
((System.Func<string>)(() => {
	var action = {{s:action}};
	var bind = KeyBinds.GetBindName(action);
	return "{\"ok\":true,\"action\":\"" + action + "\",\"bind\":\"" + bind + "\"}";
}))()