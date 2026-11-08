// recipe: console-history-b
// args: none
// returns: ok, result, error
//
// Asks the already-declared `console-history-b` class to drive the console's own input session: two submitted lines, then
// the up arrow walks the history with no completion list between them, then an empty line's Enter closes the window.
((System.Func<string>)(() => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); System.Type declaration = null; for (var i = 0; i < assemblies.Length; i++) { if (declaration == null) { try { declaration = assemblies[i].GetType("AcceptanceConsoleHistoryB", false); } catch { declaration = null; } } } if (declaration == null) { return "{\"ok\":false,\"error\":\"not-declared\"}"; } var method = declaration.GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); if (method == null) { return "{\"ok\":false,\"error\":\"no-run\"}"; } var answer = (string)method.Invoke(null, null); return "{\"ok\":true,\"result\":\"" + answer + "\"}"; }))()
