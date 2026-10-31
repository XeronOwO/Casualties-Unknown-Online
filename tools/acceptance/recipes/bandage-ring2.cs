// recipe: bandage-ring2
// args: frames=n
// serves: remote-medical-treatment-operations — starts the in-client ring driver (the `bandage-ring2` declaration), which moves the
//         staged pointer around the minigame's ring once per frame with the attack key held, so one operation can be played to its
//         end inside a single client instead of across dozens of process round trips.
// returns: ok, started, frames, error, detail
//
// Returns at once; the ring runs while the game keeps running. The verdicts are read from the operation's terminal line and from
// `bandage-ring2-state`, never from here.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var frames = (int)({{n:frames}});
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type ring = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { ring = assemblies[i].GetType("AcceptanceBandageRing2", false); } catch { ring = null; }
		if (ring != null) { break; }
	}
	if (ring == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare bandage-ring2 first\"}"; }
	var method = ring.GetMethod("Start");
	if (method == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var started = (string)method.Invoke(null, new object[] { frames });
	return "{\"ok\":true,\"started\":\"" + started + "\",\"frames\":" + frames.ToString(inv) + "}";
}))()
