// recipe: gate-arm
// args: none
// serves: world-time-local-initiation
// returns: ok, armed, startGateActive, remainingMs
//
// Host-side setup for row 4's start-gate instance. The start gate releases the moment every member is in
// the world, so a running session has no window; with one member deliberately out of the world
// (`leave-world` on that client), this calls the production arm (IWorldControl.StartStartGate) and the
// gate then holds for that member until its own 30 s fallback. The world-time guard under test reads
// exactly this state (WorldStartGate.Active -> StartGateCoordinator.WaitingForReady), so the hold is the
// game's own lifecycle, only re-armed after the run started. Arming with every member already in the
// world releases immediately (Arm's no-waiting-members branch broadcasts WorldReady) and answers
// `armed:false`, so the member must be out of the world before this runs.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	var world = services == null ? null : (services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.IWorldControl)) as CasualtiesUnknownOnline.Runtime.Session.World.IWorldControl);
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world-control\"}"; }
	var armed = false;
	var detail = "";
	try { armed = world.StartStartGate(); }
	catch (System.Exception e) { detail = e.GetType().Name + ": " + e.Message; }
	if (detail != "") { return "{\"ok\":false,\"error\":\"arm-failed\",\"detail\":\"" + detail + "\"}"; }
	return "{\"ok\":true,\"armed\":" + (armed ? "true" : "false") + ",\"startGateActive\":" + (world.StartGateActive ? "true" : "false") + ",\"remainingMs\":" + world.StartGateRemainingMs + "}";
}))()
