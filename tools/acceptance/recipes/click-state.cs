// recipe: click-state
// args: none
// returns: ok, clickGates, install, handleInput, buttonQueries, buttonDownAnswers, buttonHeldAnswers
//
// Tells whether a staged left press is being asked about at all. The acceptance assembly keeps the click-path record
// (`PlayerCamera.HandleStartDragging` entries plus the usable object it was handed) and the per-method counters for
// `Input.GetMouseButton*`; a row that hovers a usable crate, presses, and sees `clickGates` still `none` has to know whether
// the game asked the shim about the button — that separates "our press never reached the game" from "the game looked and
// the drag branch refused" (measured 2026-11-23: every patch installed, `HandleStartDragging: patched`, entries zero).
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var findType = new System.Func<string, System.Type>((simpleName) => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { System.Type[] types; try { types = assemblies[i].GetTypes(); } catch { continue; } for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } } } return null; });
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var vi = findType("VirtualInput");
	if (vi == null) { return "{\"ok\":false,\"error\":\"no-VirtualInput\"}"; }
	var patches = findType("InputQueryPatches");
	var read = new System.Func<System.Type, string, string>((type, name) => {
		if (type == null) { return "?"; }
		var property = type.GetProperty(name, Flags);
		return property == null ? "?" : System.Convert.ToString(property.GetValue(null, null), System.Globalization.CultureInfo.InvariantCulture);
	});
	var install = read(vi, "InstallReport");
	if (install.Length > 240) { install = install.Substring(0, 240); }
	return "{\"ok\":true"
		+ ",\"clickGates\":\"" + read(vi, "ClickGates").Replace("\\", "/").Replace("\"", "''") + "\""
		+ ",\"install\":\"" + install.Replace("\"", "''").Replace("\n", " | ").Replace("\r", "") + "\""
		+ ",\"handleInput\":\"" + read(patches, "HandleInputEntries") + "\""
		+ ",\"buttonQueries\":\"" + read(patches, "ButtonQueries") + "\""
		+ ",\"buttonDownAnswers\":\"" + read(patches, "ButtonDownAnswers") + "\""
		+ ",\"buttonHeldAnswers\":\"" + read(patches, "ButtonHeldAnswers") + "\"}";
}))()
