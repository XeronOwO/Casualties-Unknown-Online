// recipe: click-state
// args: none
// returns: ok, clickGates
//
// Reads the acceptance assembly's own record of `PlayerCamera.HandleStartDragging`: how often it ran and whether it was
// handed a usable object. A row that already proved the pointer hovers a usable crate still cannot tell "the click never
// reached the game" from "it reached the game with a null usable object", and those two have different owners.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var findType = new System.Func<string, System.Type>((simpleName) => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { System.Type[] types; try { types = assemblies[i].GetTypes(); } catch { continue; } for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } } } return null; });
	var vi = findType("VirtualInput");
	if (vi == null) { return "{\"ok\":false,\"error\":\"no-VirtualInput\"}"; }
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var property = vi.GetProperty("ClickGates", Flags);
	var gates = property == null ? null : property.GetValue(null, null) as string;
	return "{\"ok\":true,\"clickGates\":\"" + (gates ?? "none").Replace("\\", "/").Replace("\"", "''") + "\"}";
}))()
