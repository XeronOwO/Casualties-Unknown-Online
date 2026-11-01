// recipe: keybind-capture-state
// args: none
// returns: ok, captureWaiting, settingsActive, error, detail
//
// Answers whether the GAME's own key-bind capture is waiting for a key right now — the fact the Enter/Slash opener row turns on.
// It reads `OnlineUiKeyboardFacts.IsAKeybindCaptureWaiting`, the marker the capture itself publishes (an active SettingsMenu with a
// row whose text is exactly `...`), so this reads the same source the guard reads instead of guessing from a frame.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); var factsType = (System.Type)null; var menuType = (System.Type)null; for (var i = 0; i < assemblies.Length; i++) { if (factsType == null) { try { factsType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.OnlineUiKeyboardFacts", false); } catch { factsType = null; } } if (menuType == null) { try { menuType = assemblies[i].GetType("SettingsMenu", false); } catch { menuType = null; } } } var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic; var waiting = "unknown"; if (factsType != null) { var method = factsType.GetMethod("IsAKeybindCaptureWaiting", flags); if (method != null) { waiting = System.Convert.ToBoolean(method.Invoke(null, null)) ? "true" : "false"; } } var menuActive = "unknown"; if (menuType != null) { var found = UnityEngine.Object.FindObjectsOfType(menuType); if (found != null && found.Length > 0) { var behaviour = found[0] as UnityEngine.Behaviour; menuActive = behaviour != null && behaviour.isActiveAndEnabled ? "true" : "false"; } else { menuActive = "false"; } } return "{\"ok\":true,\"captureWaiting\":\"" + waiting + "\",\"settingsActive\":\"" + menuActive + "\"}"; }))()
