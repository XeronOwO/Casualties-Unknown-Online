// recipe: swing-now
// args: none
// returns: ok, uses, swings, error
//
// One swing through the game's own entry, driven from the client the way the syringe row drives its `OnUse` delegate
// (`docs/acceptance/lessons.md:493-495`). It exists because the staged key CANNOT reach this path on this build: the
// game's own attack method runs every frame with every gate open and its two key reads rewritten (install report:
// "HandleAttacks: patched, 2 call(s) replaced"), the staged answer reads held=true, and `Body.UseItemInHand` is still
// never called (measured 2026-11-22). The row keeps the player-visible outcome — the block's damage and crack stage, the
// member's view of the same cell — as what it asserts.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var error = "";
	try { camera.body.UseItemInHand(); }
	catch (System.Exception exception) { error = exception.GetType().Name + ": " + exception.Message; }
	var count = new System.Func<string>(() => {
		var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var a = 0; a < assemblies.Length; a++)
		{
			System.Type[] types;
			try { types = assemblies[a].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++)
			{
				if (types[t].Name != "VirtualInput") { continue; }
				var used = types[t].GetProperty("Uses", flags);
				var swung = types[t].GetProperty("Swings", flags);
				var hurt = types[t].GetProperty("Hurts", flags);
				var usedValue = used == null ? -1L : System.Convert.ToInt64(used.GetValue(null, null), inv);
				var swingValue = swung == null ? -1L : System.Convert.ToInt64(swung.GetValue(null, null), inv);
				var hurtValue = hurt == null ? -1L : System.Convert.ToInt64(hurt.GetValue(null, null), inv);
				return "{\"uses\":" + usedValue.ToString(inv) + ",\"swings\":" + swingValue.ToString(inv) + ",\"hurts\":" + hurtValue.ToString(inv) + "}";
			}
		}
		return "{\"uses\":-1,\"swings\":-1}";
	});
	var counters = count();
	return "{\"ok\":true,\"counters\":" + counters + ",\"error\":\"" + error.Replace("\"", "'") + "\"}";
}))()
