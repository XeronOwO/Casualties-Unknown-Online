// eval declaration check - the presence probe the driver's `declare` action sends before and after it
// loads a declaration. The single placeholder below is replaced by the name a declaration file's own
// `// declares:` line carries, so this one committed expression serves every declaration.
//
// Why a probe exists at all: the evaluator takes a type declaration at most once per client, and a
// repeat answers its own "already defined" error instead of a verdict - so a run that sends a
// declaration twice must not read that as a failure. The probe makes the action idempotent, and its
// answer is the capability's evidence line in a run: `declared: false` means the type was not in this
// client's evaluator, `declared: true` names the assembly it lives in.
//
// Same evaluator as every recipe: C# 7.x, no usings, fully qualified names, one trailing expression,
// and every guard on one line.
((System.Func<string>)(() => {
	var wanted = {{s:type}};
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		System.Type found = null;
		try { found = assemblies[i].GetType(wanted, false); } catch { found = null; }
		if (found != null) { return "{\"ok\":true,\"declared\":true,\"type\":\"" + wanted + "\",\"assembly\":\"" + found.Assembly.GetName().Name + "\"}"; }
	}
	return "{\"ok\":true,\"declared\":false,\"type\":\"" + wanted + "\"}";
}))()
