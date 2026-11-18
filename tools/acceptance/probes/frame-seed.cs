// recipe: frame-seed
// args: mode=s frames=n
// returns: ok, mode, frames, started, error, detail
//
// Starts the `frame-seed` declaration: it re-asserts a game state on EVERY frame for `frames` frames. It exists because some of the game's own routines demand a
// state that its simulation keeps moving — `Body.HandleDogWaterShaking` starts the water-shake coroutine (and its `dogshake` clip) only after `wetness > 30 &&
// temperature < 36.6 && !wasWater` has held for twelve seconds and resets the moment it stops. A one-shot write cannot hold that; a per-frame one does.
//
// Declare once per client (`-Action declare -Declare frame-seed`) before the first call.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var mode = {{s:mode}};
	var frames = (int)({{n:frames}});
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type seed = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { seed = assemblies[i].GetType("AcceptanceFrameSeed", false); } catch { seed = null; }
		if (seed != null) { break; }
	}
	if (seed == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare frame-seed first\"}"; }
	var method = seed.GetMethod("Start");
	if (method == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var started = (string)method.Invoke(null, new object[] { mode, frames });
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"frames\":" + frames.ToString(inv) + ",\"started\":\"" + started + "\"}";
}))()
