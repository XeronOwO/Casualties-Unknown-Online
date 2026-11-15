// recipe: run-ending-a
// args: mode=s kind=s
// returns: ok, result, error
//
// Reflection only: the tracker and the game's ending methods are found by name, so the evaluator never
// needs a typed reference to our own runtime or to Assembly-CSharp. The declaration form cannot persist
// a type across the Roslyn evaluator's separate submissions, so the entire operation lives in this eval.
((System.Func<string>)(() => {
	var any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Reflection.Assembly runtime = null;
	System.Reflection.Assembly game = null;
	for (var i = 0; i < assemblies.Length; i++) {
		var name = assemblies[i].GetName().Name;
		if (name == "CasualtiesUnknownOnline.Runtime") { runtime = assemblies[i]; }
		if (name == "Assembly-CSharp") { game = assemblies[i]; }
	}
	if (runtime == null) { return "{\"ok\":false,\"error\":\"no-runtime-assembly\"}"; }
	var tracker = runtime.GetType("CasualtiesUnknownOnline.Runtime.Session.World.RunEndingTracker", false);
	if (tracker == null) { return "{\"ok\":false,\"error\":\"no-tracker\"}"; }
	var mode = {{s:mode}};
	var kind = {{s:kind}};
	if (mode == "call") {
		if (game == null) { return "{\"ok\":false,\"error\":\"no-game-assembly\"}"; }
		var worldType = game.GetType("WorldGeneration", false);
		if (worldType == null) { return "{\"ok\":false,\"error\":\"no-worldgeneration\"}"; }
		var worldField = worldType.GetField("world", any);
		var world = worldField == null ? null : worldField.GetValue(null);
		if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
		var methodName = kind == "breach" ? "SurfaceBreachCutscene" : "MercEnding";
		var method = worldType.GetMethod(methodName, any, null, System.Type.EmptyTypes, null);
		if (method == null) { return "{\"ok\":false,\"error\":\"no-" + methodName + "\"}"; }
		method.Invoke(world, null);
	}
	else if (mode == "reset") {
		var clear = tracker.GetMethod("Clear", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
		if (clear != null) { clear.Invoke(null, null); }
	}
	var current = tracker.GetProperty("Current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
	var fromAuthority = tracker.GetProperty("FromAuthority", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
	var answer = "ending=" + (current == null ? "?" : current.GetValue(null, null))
		+ "|authority=" + (fromAuthority == null ? "?" : fromAuthority.GetValue(null, null))
		+ "|mode=" + mode;
	return "{\"ok\":true,\"result\":\"" + answer + "\"}";
}))()
