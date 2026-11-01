// recipe: report-watch-start
// args: none
// returns: ok, patched, error, detail
//
// Installs the `report-watch` declaration's prefix on the adapter's own `CharacterSoundSync.Report`, so every character-sound report the client makes is
// recorded with its arguments. Declare once per client (`-Action declare -Declare report-watch`) before the first call; read with `report-watch-read`.
((System.Func<string>)(() => {
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type watch = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { watch = assemblies[i].GetType("AcceptanceReportWatch", false); } catch { watch = null; }
		if (watch != null) { break; }
	}
	if (watch == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare report-watch first\"}"; }
	var start = watch.GetMethod("Start");
	if (start == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var patched = (string)start.Invoke(null, null);
	return "{\"ok\":true,\"patched\":\"" + patched + "\"}";
}))()
