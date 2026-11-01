// recipe: pointer-shake
// args: mode=s x1=n y1=n x2=n y2=n frames=n drift=n
// returns: ok, running, first, second, framesPerSide, error
//
// Starts or stops a pointer that alternates between two points ON CONSECUTIVE FRAMES. A minigame that accumulates from the hand's
// frame-to-frame velocity cannot be driven from the batch script: each staged position costs one round trip, so the hand settles
// and the velocity the game reads stays near zero (measured 2026-11-02 on the amputation minigame). The shake runs inside the
// client, so the game sees what a held, moving mouse produces.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	if (mode == "stop") { return CasualtiesUnknownOnline.GameAdapter.Acceptance.PointerShake.Stop(); }
	return CasualtiesUnknownOnline.GameAdapter.Acceptance.PointerShake.Start((float){{n:x1}}, (float){{n:y1}}, (float){{n:x2}}, (float){{n:y2}}, (int){{n:frames}}, (float){{n:drift}});
}))()