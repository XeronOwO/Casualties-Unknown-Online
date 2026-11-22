// recipe: key-edges
// args: none
// returns: ok, edges
//
// The last key edges a batch staged, oldest first, as `Key:down@frame`. A row that watched one speed survive another speed's press needs to
// know whether its own batch re-staged that key or never released it — measured 2026-11-23: the host's own game issued Normal and SuperFast
// in the same frame, so the row has to name who still held each bind before it can blame the product or its own staging.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var findType = new System.Func<string, System.Type>((simpleName) => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { System.Type[] types; try { types = assemblies[i].GetTypes(); } catch { continue; } for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } } } return null; });
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var vi = findType("VirtualInput");
	if (vi == null) { return "{\"ok\":false,\"error\":\"no-VirtualInput\"}"; }
	var property = vi.GetProperty("KeyEdges", Flags);
	if (property == null) { return "{\"ok\":false,\"error\":\"no-KeyEdges\"}"; }
	var edges = System.Convert.ToString(property.GetValue(null, null), System.Globalization.CultureInfo.InvariantCulture) ?? "";
	return "{\"ok\":true,\"edges\":\"" + edges.Replace("\"", "''") + "\"}";
}))()
