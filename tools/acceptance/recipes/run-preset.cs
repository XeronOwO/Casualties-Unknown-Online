// recipe: run-preset
// args: name=s
// returns: ok, preset, error
//
// Selects the game's own pre-run preset that the HOST applies when it starts a run; an empty name clears
// the override. The static lives in
// CUO's GameAdapter assembly, so it is found by name across the loaded assemblies; the game's own
// RunSettings.GetPreset validates the name when StartRun applies it.
((System.Func<string>)(() => {
	var name = {{s:name}};
	const System.Reflection.BindingFlags StaticAny = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	System.Type presetType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && presetType == null; i++) {
		try { presetType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Run.RunPresetDecision", false); } catch { presetType = null; }
	}
	if (presetType == null) { return "{\"ok\":false,\"error\":\"no-run-preset-decision\"}"; }
	var property = presetType.GetProperty("Preset", StaticAny);
	if (property == null) { return "{\"ok\":false,\"error\":\"no-preset-property\"}"; }
	property.SetValue(null, name.Length == 0 ? null : name, null);
	return "{\"ok\":true,\"preset\":\"" + name + "\"}";
}))()
