// recipe: bandage-hand
// args: mode=s radius=n step=n
// serves: 1.0-rebuild-each-acceptance-row-from-the-game (row 7, medical-bandage)
// returns: ok, mode, active, frames, angle
//
// Starts or stops the acceptance assembly's per-frame placement of the bandage wrap's hand. The game computes the
// hand's angle from `Minigame.game.handPos` (`BandageMinigame.cs:107`), and that hand is a damped body pulled at the
// platform pointer (`MinigameBase.UpdateHandPhysics`), so a staged pointer never carries it: on the real pair the
// roll advanced one twenty-degree step in a whole gesture and the wrap never finished (2026-11-21/22). Placing the
// hand on the ring one step a frame leaves every other condition of the wrap — the click-start window, the ratchet,
// the finish under ninety degrees — to the game's own code.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var mode = {{s:mode}};
	var radius = {{n:radius}};
	var step = {{n:step}};
	var findType = new System.Func<string, System.Type>((simpleName) => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { System.Type[] types; try { types = assemblies[i].GetTypes(); } catch { continue; } for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } } } return null; });
	var type = findType("BandageHandDrive");
	if (type == null) { return "{\"ok\":false,\"error\":\"the hand drive is not in this build\"}"; }
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	if (mode == "start") { type.GetMethod("Start", Flags).Invoke(null, new object[] { (float)radius, (float)step }); }
	else if (mode == "stop") { type.GetMethod("Stop", Flags).Invoke(null, new object[0]); }
	else { return "{\"ok\":false,\"error\":\"mode must be start or stop\"}"; }
	var active = (bool)type.GetProperty("Active", Flags).GetValue(null, null);
	var frames = (int)type.GetProperty("Frames", Flags).GetValue(null, null);
	var angle = (float)type.GetProperty("Angle", Flags).GetValue(null, null);
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"active\":" + (active ? "true" : "false") + ",\"frames\":" + frames.ToString(inv) + ",\"angle\":" + angle.ToString("0.###", inv) + "}";
}))()
