// recipe: bandage-rub-state
// args: none
// returns: ok, starts, saw, drove, running, mainNull, other, error, detail
//
// Reads the counters the `bandage-rub6` declaration keeps: how many times the rub was started, how many frames it saw a bandage
// minigame, how many frames it drove the hand, whether it is still running, how many frames the game's minigame singleton was
// absent, and the type name of the last minigame it saw instead. It exists because the declaration's own `Debug.Log` reaches
// neither BepInEx's log nor Unity's Player.log, which made "the rub drove nothing" and "the rub never ran" indistinguishable.
//
// The evaluator refuses a nested delegate that CAPTURES a local, so the binding-flags expression is written out inside each
// lambda instead of being read from a variable, and every read is null-safe: a client holding an older copy of the declaration
// reports -1 (or an empty string) for a counter that copy does not have.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type rub = null;
	for (var i = 0; i < assemblies.Length; i++) {
		System.Type candidate = null;
		try { candidate = assemblies[i].GetType("AcceptanceBandageRub6", false); } catch { candidate = null; }
		if (candidate != null && candidate.GetField("Starts", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) != null) { rub = candidate; break; }
	}
	if (rub == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare bandage-rub6 first\"}"; }

	var num = new System.Func<System.Type, string, int>((type, name) => {
		var info = type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		if (info == null) { return -1; }
		var value = info.GetValue(null);
		return value is bool ? (((bool)value) ? 1 : 0) : System.Convert.ToInt32(value);
	});
	var text = new System.Func<System.Type, string, string>((type, name) => {
		var info = type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		if (info == null) { return ""; }
		var value = info.GetValue(null);
		return value == null ? "" : value.ToString();
	});

	return "{\"ok\":true,\"starts\":" + num(rub, "Starts").ToString(inv)
		+ ",\"saw\":" + num(rub, "Saw").ToString(inv)
		+ ",\"drove\":" + num(rub, "Drove").ToString(inv)
		+ ",\"running\":" + (num(rub, "Running") > 0 ? "true" : "false")
		+ ",\"mainNull\":" + num(rub, "MainNull").ToString(inv)
		+ ",\"actions\":" + num(rub, "Actions").ToString(inv)
		+ ",\"other\":\"" + text(rub, "Other") + "\"}";
}))()
