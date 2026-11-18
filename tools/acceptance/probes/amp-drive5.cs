// recipe: amp-drive5
// args: frames=n amplitude=n
// returns: ok, started, frames, amplitude, error, detail
//
// Starts the `amp-drive5` declaration: it alternates the staged pointer left and right across the screen centre every frame, which is the horizontal
// sawing motion `AmputationMinigame.PhysicsUpdate` integrates (`cutProgress += |handVelocity.x| * 5E-05f` while `cutting`). Returns at once; the verdict is
// the limb's own `dismembered` flag and the peer's clip reading.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var frames = (int)({{n:frames}});
	var amplitude = (float)({{n:amplitude}});
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type drive = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { drive = assemblies[i].GetType("AcceptanceAmpDrive5", false); } catch { drive = null; }
		if (drive != null) { break; }
	}
	if (drive == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare amp-drive5 first\"}"; }
	var method = drive.GetMethod("Start");
	if (method == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var started = (string)method.Invoke(null, new object[] { frames, amplitude });
	return "{\"ok\":true,\"started\":\"" + started + "\",\"frames\":" + frames.ToString(inv) + ",\"amplitude\":" + amplitude.ToString(inv) + "}";
}))()
