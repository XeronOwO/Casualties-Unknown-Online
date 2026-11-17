// recipe: pointer-circle
// args: mode=s cx=n cy=n radius=n deg=n frames=n
// returns: ok, running, centre, radius, degreesPerFrame, frames, angle, framesLeft, x, y, error, detail
//
// Drives the bandage/splint minigame's gesture: the hand has to go AROUND the roll, not past it. mode=start begins a circular pointer
// path (one step per frame, inside the client - see PointerCircle), mode=read reports the angle drawn so far, mode=stop ends it.
// `BandageMinigame.PhysicsUpdate` advances `bandageAngle` only while `holdingBandage` (the hand's direction within about 25 degrees of
// the roll) and sets `didWrap` at a full turn, so the caller also holds the attack button (`virtual-input mode=press button=0`).
//
// The component is internal to the batch-only acceptance assembly, which the evaluator's dynamic assembly cannot reference: the
// calls go through reflection with non-public binding flags, the same way the VirtualInput recipes reach that type.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var cx = (float){{n:cx}};
	var cy = (float){{n:cy}};
	var radius = (float){{n:radius}};
	var deg = (float){{n:deg}};
	var frames = (int){{n:frames}};
	var findType = new System.Func<string, System.Type>((simpleName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } }
		}
		return null;
	});
	var circle = findType("PointerCircle");
	if (circle == null) { return "{\"ok\":false,\"error\":\"no-PointerCircle\"}"; }
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var report = circle.GetMethod("Report", Flags);
	if (mode == "read") { return (string)report.Invoke(null, null); }
	if (mode == "stop") { return (string)circle.GetMethod("Stop", Flags).Invoke(null, null); }
	var start = circle.GetMethod("Start", Flags);
	return (string)start.Invoke(null, new object[] { cx, cy, radius, deg, frames });
}))()
