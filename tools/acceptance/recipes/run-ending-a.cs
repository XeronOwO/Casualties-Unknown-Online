// recipe: run-ending-a
// args: mode=s kind=s
// returns: ok, result, error
//
// Asks the already-declared `run-ending-a` class to read the run's ending tracker, or to call one of the
// game's own ending methods (which the adapter's patch records).
((System.Func<string>)(() => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); System.Type declaration = null; for (var i = 0; i < assemblies.Length; i++) { if (declaration == null) { try { declaration = assemblies[i].GetType("AcceptanceRunEndingA", false); } catch { declaration = null; } } } if (declaration == null) { return "{\"ok\":false,\"error\":\"not-declared\"}"; } var method = declaration.GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); if (method == null) { return "{\"ok\":false,\"error\":\"no-run\"}"; } var answer = (string)method.Invoke(null, new object[] { {{s:mode}}, {{s:kind}} }); return "{\"ok\":true,\"result\":\"" + answer + "\"}"; }))()
