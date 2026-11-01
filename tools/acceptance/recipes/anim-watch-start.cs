// recipe: anim-watch-start
// args: none
// returns: ok, patched, error, detail
//
// Installs the `anim-watch` declaration's prefix on the adapter's own `CharacterAttackAnimSync.OnReceived`, so every character-sound report the client makes is
// recorded with its arguments. Declare once per client (`-Action declare -Declare anim-watch`) before the first call; read with `anim-watch-read`.
((System.Func<string>)(() => {
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type watch = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { watch = assemblies[i].GetType("AcceptanceAnimWatch", false); } catch { watch = null; }
		if (watch != null) { break; }
	}
	if (watch == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare anim-watch first\"}"; }
	var start = watch.GetMethod("Start");
	if (start == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var patched = (string)start.Invoke(null, null);
	return "{\"ok\":true,\"patched\":\"" + patched + "\"}";
}))()
