// recipe: key-hold
// args: bind=s action=s
// serves: container-move-snapshot-only-sync row A1g (the game's container-expansion branch reads
//         Input.GetKey(KeyBinds.GetBind("expanddesc")), so the kind needs a HELD key)
// requires: window-key - the eval declaration this recipe calls, sent once per client before the first
//           key-hold step: drive-in-process.ps1 -Action declare -Declare window-key
// returns: ok, bind, key, action, virtualKey, scanCode, window, foreground, isForeground, posted,
//          heldAtStart, heldAtEnd, osKeyAtStart, osKeyAtEnd, error, detail
//
// Holds, releases and reads one of the GAME'S OWN key binds from inside the client's process:
//
//   - `action=down` and `action=up` queue one key message on the client's OWN window (never another
//     window, never the OS input queue, no focus change - see the window-key declaration) and report what
//     they queued. `ok` means the client's window accepted the message; the state it produces lands on
//     the client's own next frame, which is AFTER this input returns - measured on batch `20261006-b`'s
//     first smoke: an input that sleeps on the evaluator's thread cannot see its own message land, and
//     the very next input reads the new state. **The hold's verdict is therefore the following
//     `action=read` step**, and a run records that step's answer for every hold and every release: a
//     half-verified hold leaves a modifier stuck down for the rest of the session, which batch
//     `20261006-a` measured on a release that missed its message flags.
//   - `action=read` posts nothing and is the checkpoint: it reports the bind's current state, the OS key
//     state and whether the client's window is the foreground one. `heldAtEnd` of a read IS the verdict.
//
// The virtual key comes from the game's own KeyCode, because Unity's KeyCode is not a Windows virtual
// key (LeftShift is 304, VK_LSHIFT is 0xA0). The mapping below covers every key the game's own binds
// name, plus the navigation keys; a bind this build does not map is refused by name rather than posted
// as a guess, and a bind on a MOUSE button is refused outright - this recipe holds keys, and the run
// never drives the mouse (`PlayerCamera` binds attack and iteminteract to Mouse0/Mouse1).
((System.Func<string>)(() => {
	var bind = {{s:bind}};
	var action = {{s:action}};
	if (action != "down" && action != "up" && action != "read") { return "{\"ok\":false,\"error\":\"bad-action\",\"detail\":\"action must be down, up or read\"}"; }
	var key = KeyBinds.GetBind(bind);
	if (key == UnityEngine.KeyCode.None) { return "{\"ok\":false,\"error\":\"no-bind\",\"detail\":\"the game has no key bound to '" + bind + "'\"}"; }
	var code = (int)key;
	var virtualKey = -1;
	if (code >= 97 && code <= 122) { virtualKey = code - 32; }                     // A-Z (Unity: 97-122, VK: 0x41-0x5A)
	else if (code >= 48 && code <= 57) { virtualKey = code; }                      // Alpha0-Alpha9 are the VK digits
	else if (code >= 282 && code <= 296) { virtualKey = code - 282 + 112; }        // F1-F15 -> VK_F1..VK_F15
	else if (code >= 256 && code <= 265) { virtualKey = code - 256 + 96; }         // Keypad0-Keypad9 -> VK_NUMPAD0..
	else if (code == 8) { virtualKey = 8; }                                        // Backspace
	else if (code == 9) { virtualKey = 9; }                                        // Tab
	else if (code == 13) { virtualKey = 13; }                                      // Return
	else if (code == 27) { virtualKey = 27; }                                      // Escape
	else if (code == 32) { virtualKey = 32; }                                      // Space
	else if (code == 96) { virtualKey = 192; }                                     // BackQuote -> VK_OEM_3
	else if (code == 127) { virtualKey = 46; }                                     // Delete -> VK_DELETE
	else if (code == 273) { virtualKey = 38; }                                     // UpArrow
	else if (code == 274) { virtualKey = 40; }                                     // DownArrow
	else if (code == 275) { virtualKey = 39; }                                     // RightArrow
	else if (code == 276) { virtualKey = 37; }                                     // LeftArrow
	else if (code == 303) { virtualKey = 161; }                                    // RightShift -> VK_RSHIFT
	else if (code == 304) { virtualKey = 160; }                                    // LeftShift -> VK_LSHIFT
	else if (code == 305) { virtualKey = 163; }                                    // RightControl -> VK_RCONTROL
	else if (code == 306) { virtualKey = 162; }                                    // LeftControl -> VK_LCONTROL
	else if (code == 307) { virtualKey = 165; }                                    // RightAlt -> VK_RMENU
	else if (code == 308) { virtualKey = 164; }                                    // LeftAlt -> VK_LMENU
	if (code >= 323 && code <= 329) { return "{\"ok\":false,\"error\":\"mouse-bind-refused\",\"detail\":\"the game binds '" + bind + "' to " + key.ToString() + "; this recipe holds keys only and the run never drives the mouse\"}"; }
	if (virtualKey < 0) { return "{\"ok\":false,\"error\":\"unmapped-key\",\"detail\":\"no virtual key is mapped for " + key.ToString() + " (KeyCode " + code.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")\"}"; }
	// The message carries the scan code of the PHYSICAL key, and the client's own keyboard layout answers
	// for the virtual key itself - left/right-specific codes included, whose scan codes differ from each
	// other (VK_LSHIFT is 0x2A, VK_RSHIFT is 0x36), so the key the bind resolved to is the one asked for.
	var scanCode = AcceptanceWindowKey.ScanCodeOf(virtualKey);
	var window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
	if (window == System.IntPtr.Zero) { return "{\"ok\":false,\"error\":\"no-window\",\"detail\":\"this client reports no main window handle to post to\"}"; }
	var foreground = AcceptanceWindowKey.ForegroundWindow();
	var heldAtStart = UnityEngine.Input.GetKey(key);
	var osKeyAtStart = AcceptanceWindowKey.OsKeyState(virtualKey);
	var posted = false;
	if (action != "read") {
		posted = action == "down"
			? AcceptanceWindowKey.QueueKeyDown(window, virtualKey, scanCode)
			: AcceptanceWindowKey.QueueKeyUp(window, virtualKey, scanCode);
	}
	var heldAtEnd = UnityEngine.Input.GetKey(key);
	var osKeyAtEnd = AcceptanceWindowKey.OsKeyState(virtualKey);
	// `ok` is "the client's window took the message" for a hold or a release, and always true for a read:
	// the state a message produces is read by the NEXT input (see this file's header), so a hold is judged
	// by the read step that follows it and never by the reading taken here.
	var ok = action == "read" || posted;
	var json = new System.Text.StringBuilder();
	json.Append("{\"ok\":").Append(ok ? "true" : "false");
	json.Append(",\"bind\":\"").Append(bind).Append("\"");
	json.Append(",\"key\":\"").Append(key.ToString()).Append("\"");
	json.Append(",\"action\":\"").Append(action).Append("\"");
	json.Append(",\"virtualKey\":").Append(virtualKey.ToString(System.Globalization.CultureInfo.InvariantCulture));
	json.Append(",\"scanCode\":").Append(scanCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
	json.Append(",\"window\":").Append(window.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
	json.Append(",\"foreground\":").Append(foreground.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
	json.Append(",\"isForeground\":").Append(foreground == window ? "true" : "false");
	json.Append(",\"posted\":").Append(posted ? "true" : "false");
	json.Append(",\"heldAtStart\":").Append(heldAtStart ? "true" : "false");
	json.Append(",\"heldAtEnd\":").Append(heldAtEnd ? "true" : "false");
	json.Append(",\"osKeyAtStart\":").Append(osKeyAtStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
	json.Append(",\"osKeyAtEnd\":").Append(osKeyAtEnd.ToString(System.Globalization.CultureInfo.InvariantCulture));
	if (!ok) {
		json.Append(",\"error\":\"post-refused\"");
		json.Append(",\"detail\":\"the client's own window refused the ").Append(action).Append(" message (the declaration's queue call answered false)\"");
	}
	json.Append("}");
	return json.ToString();
}))()
