// recipe: down-trail
// args: none
// serves: medical-bandage
// returns: ok, trail, keyDownAnswers, keyQueries
//
// The last DOWN questions the replaced input call sites asked, oldest first, as `key@frame=answer`.
// `keyDownAnswers` counts how many were answered true, but not WHICH call site took the edge: the bandage row's
// hand kept reading `handStartedClicking:false` while the counter moved (measured 2026-11-26, band-21), so the
// next reading has to name the frame each probe happened in.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var findType = new System.Func<string, System.Type>((simpleName) => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { System.Type[] types; try { types = assemblies[i].GetTypes(); } catch { continue; } for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } } } return null; });
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var patches = findType("InputQueryPatches");
	if (patches == null) { return "{\"ok\":false,\"error\":\"no-InputQueryPatches\"}"; }
	var read = new System.Func<string, string>((name) => {
		var property = patches.GetProperty(name, Flags);
		return property == null ? "?" : System.Convert.ToString(property.GetValue(null, null), System.Globalization.CultureInfo.InvariantCulture);
	});
	var trail = read("TrailText");
	if (trail.Length > 1200) { trail = trail.Substring(trail.Length - 1200); }
	return "{\"ok\":true"
		+ ",\"trail\":\"" + trail.Replace("\\", "/").Replace("\"", "''") + "\""
		+ ",\"keyDownAnswers\":\"" + read("KeyDownAnswers") + "\""
		+ ",\"keyQueries\":\"" + read("KeyQueries") + "\"}";
}))()
