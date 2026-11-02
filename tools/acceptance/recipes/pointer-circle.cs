// recipe: pointer-circle
// args: mode=s cx=n cy=n radius=n deg=n frames=n
// returns: ok, running, centre, radius, degreesPerFrame, frames, angle, framesLeft, error, detail
//
// Drives the bandage/splint minigame's gesture: the hand has to go AROUND the roll, not past it. mode=start begins a circular pointer
// path (one step per frame, inside the client - see PointerCircle), mode=read reports the angle drawn so far, mode=stop ends it.
// `BandageMinigame.PhysicsUpdate` advances `bandageAngle` only while `holdingBandage` (the hand's direction within ~25 degrees of the
// roll) and sets `didWrap` at a full turn, so the caller also holds the attack button.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	if (mode == "read") { return PointerCircle.Report(); }
	if (mode == "stop") { return PointerCircle.Stop(); }
	var started = PointerCircle.Start((float){{n:cx}}, (float){{n:cy}}, (float){{n:radius}}, (float){{n:deg}}, (int){{n:frames}});
	return started;
}))()
