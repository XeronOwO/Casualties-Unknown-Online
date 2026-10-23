// recipe: key-tap
// args: vk=n action=s
// requires: window-key - the eval declaration this recipe calls, sent once per client before the first
//           key-tap step: drive-in-process.ps1 -Action declare -Declare window-key
// serves: enter-key-opens-plain-chat
// returns: ok, action, virtualKey, scanCode, window, isForeground, posted, error, detail
//
// Taps a raw Windows virtual key through the `window-key` declaration, for keys that are not one of the
// game's own bind names - the Return/Slash the CUO opener watches, for instance. `key-hold` resolves a
// BIND (`KeyBinds.GetBind`); this resolves the virtual-key code itself. Mouse-button virtual keys (1-6)
// are refused: the run never drives the mouse, and key-hold refuses a mouse bind for the same reason. It
// posts on the client's OWN window, exactly as key-hold does, and carries the same properties: the OS
// input queue is never touched, no other window is addressed, and the sender reads back only what it
// queued. Pair `action=down` and `action=up` with a state read between them; the state a message produces
// is read by the client's next input.
((System.Func<string>)(() => {
	var vk = {{n:vk}};
	var action = {{s:action}};
	if (vk <= 6 || vk > 254) { return "{\"ok\":false,\"error\":\"bad-vk\",\"detail\":\"the virtual key must be 7-254; 1-6 are mouse buttons and this run never drives the mouse\"}"; }
	if (action != "down" && action != "up") { return "{\"ok\":false,\"error\":\"bad-action\",\"detail\":\"action must be down or up\"}"; }
	var scanCode = AcceptanceWindowKey.ScanCodeOf(vk);
	var window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
	if (window == System.IntPtr.Zero) { return "{\"ok\":false,\"error\":\"no-window\",\"detail\":\"this client reports no main window handle to post to\"}"; }
	var foreground = AcceptanceWindowKey.ForegroundWindow();
	var posted = action == "down"
		? AcceptanceWindowKey.QueueKeyDown(window, vk, scanCode)
		: AcceptanceWindowKey.QueueKeyUp(window, vk, scanCode);
	return "{\"ok\":" + (posted ? "true" : "false")
		+ ",\"action\":\"" + action + "\""
		+ ",\"virtualKey\":" + vk.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"scanCode\":" + scanCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"window\":" + window.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"isForeground\":" + (foreground == window ? "true" : "false")
		+ ",\"posted\":" + (posted ? "true" : "false") + "}";
}))()
