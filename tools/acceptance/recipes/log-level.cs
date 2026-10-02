// recipe: log-level
// args: level=s
// serves: runtime-entity-creation-rejection row 8 (the non-reporter's ignore line is Debug) and every run that needs a per-client log level
// returns: ok, level, error, detail
//
// Sets the CUO log level through the same editor the Preferences control writes — the live seam the
// game's own UI uses. BepInEx 5.4 does not reload a directly edited .cfg, so an in-process set is the
// only way to raise one client's verbosity for a window. One invocation sets one level on one client;
// restore Information when the window closes.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var level = {{s:level}};
	System.Type editorType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		editorType = assemblies[i].GetType("CasualtiesUnknownOnline.LoggingConfigEditor", false);
		if (editorType != null) { break; }
	}
	if (editorType == null) { return "{\"ok\":false,\"error\":\"no-editor-type\"}"; }
	var editor = services.GetService(editorType);
	if (editor == null) { return "{\"ok\":false,\"error\":\"no-editor\"}"; }
	var setMethod = editorType.GetMethod("Set", all);
	if (setMethod == null) { return "{\"ok\":false,\"error\":\"no-set-method\"}"; }
	setMethod.Invoke(editor, new object[] { level });
	var currentProperty = editorType.GetProperty("Current", all);
	var current = currentProperty != null ? (string)currentProperty.GetValue(editor, null) : "?";
	return "{\"ok\":true,\"level\":\"" + current + "\"}";
}))()
