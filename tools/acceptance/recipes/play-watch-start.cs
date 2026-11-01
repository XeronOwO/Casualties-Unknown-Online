// recipe: play-watch-start
// args: none
// returns: ok, patched, error, detail
//
// Installs the `play-watch` declaration's prefix on the game's `Sound.Play(AudioClip, …)` overload, so every clip this client actually asks the engine to play
// is recorded by name. Declare once per client (`-Action declare -Declare play-watch`) first; read with `play-watch-read`.
((System.Func<string>)(() => {
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type watch = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { watch = assemblies[i].GetType("AcceptancePlayWatch", false); } catch { watch = null; }
		if (watch != null) { break; }
	}
	if (watch == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare play-watch first\"}"; }
	var start = watch.GetMethod("Start");
	if (start == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var patched = (string)start.Invoke(null, null);
	return "{\"ok\":true,\"patched\":\"" + patched + "\"}";
}))()
