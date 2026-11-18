// recipe: run-ending-b
// args: none
// returns: ok, result, error
((System.Func<string>)(() => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); System.Type declaration = null; for (var i = 0; i < assemblies.Length; i++) { if (declaration == null) { try { declaration = assemblies[i].GetType("AcceptanceRunEndingB", false); } catch { declaration = null; } } } if (declaration == null) { return "{\"ok\":false,\"error\":\"not-declared\"}"; } var method = declaration.GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); var answer = (string)method.Invoke(null, null); return "{\"ok\":true,\"result\":\"" + answer + "\"}"; }))()
