// recipe: shrapnel-observe5
// args: none
// returns: ok, result, error
//
// Reads the observer's own piece rows from the already-declared `shrapnel-observe4` class (`index:y`). The host writes
// `8000 + index` into a removed piece's y, so a removed piece reads in the 8000s while a live piece keeps its own y.
((System.Func<string>)(() => {
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type declaration = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (declaration == null) { try { declaration = assemblies[i].GetType("AcceptanceShrapnelObserve4", false); } catch { declaration = null; } }
	}
	if (declaration == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare shrapnel-observe4 first\"}"; }
	var method = declaration.GetMethod("Positions");
	if (method == null) { return "{\"ok\":false,\"error\":\"no-positions\"}"; }
	string observerRows;
	try { observerRows = (string)method.Invoke(null, null); } catch (System.Exception ex) { return "{\"ok\":false,\"error\":\"" + ex.GetType().Name + "\"}"; }
	return "{\"ok\":true,\"result\":\"" + observerRows.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
}))()
