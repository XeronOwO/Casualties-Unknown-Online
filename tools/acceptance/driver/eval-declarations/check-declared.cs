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
// Two lookups, because the evaluator moved from Mono.CSharp to Roslyn in the 1.0.2 game build. Roslyn
// compiles every script into a numbered submission class, so a declaration's type is not a top-level
// type of an assembly: it is nested (`Submission#13+AcceptanceBandageRub6`, assembly `ℛ*<guid>#8-13`,
// measured 2026-11-18). The exact-name lookup still answers first, for a type that does live at the top
// level of an assembly; the simple-name scan behind it is what finds a Roslyn submission type. The scan
// walks every type of every loaded assembly - 33,734 types over 238 assemblies in 31 ms on the measured
// client - so it is cheap enough to run twice per declaration.
//
// Same evaluator as every recipe: C# 7.x, no usings, fully qualified names, one trailing expression,
// and every guard on one line.
((System.Func<string>)(() => {
	var wanted = {{s:type}};
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type found = null;
	var assemblyName = "";
	for (var i = 0; i < assemblies.Length; i++) {
		try { found = assemblies[i].GetType(wanted, false); } catch { found = null; }
		if (found != null) { assemblyName = found.Assembly.GetName().Name; break; }
	}
	for (var i = 0; i < assemblies.Length && found == null; i++) {
		System.Type[] types = null;
		try { types = assemblies[i].GetTypes(); } catch { types = null; }
		if (types == null) { continue; }
		for (var j = 0; j < types.Length; j++) {
			if (types[j].Name == wanted) { found = types[j]; assemblyName = assemblies[i].GetName().Name; break; }
		}
	}
	if (found != null) { return "{\"ok\":true,\"declared\":true,\"type\":\"" + wanted + "\",\"assembly\":\"" + assemblyName + "\"}"; }
	return "{\"ok\":true,\"declared\":false,\"type\":\"" + wanted + "\"}";
}))()
