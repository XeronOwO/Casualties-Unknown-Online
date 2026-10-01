// recipe: world-time-request
// args: speed=s
// serves: world-time-local-initiation
// returns: ok, requested, sent, role, sessionActive, inWorld, gateWaiting, timeScale, curTimeScale
//
// Sends one WorldTimeRequest through the production send a guest's own report uses — the
// IWorldTimeControl.SendRequest call WorldTimeSync.BeginLocalFirst makes for the report half
// (WorldTimeChannel.SendRequest -> NetMsg.WorldTimeRequest). The local-first half itself is the native
// SetTimeScale call, unreachable from the evaluator and covered by the time-scale recipe; this recipe
// exists for the three refusal instances the native keys cannot reach: from the lobby (no world, no local
// camera), under a held gate, and with a speed no guest may request (the sleep-owned
// UnconsciousFast/DyingFast). It needs no PlayerCamera and reports the state at the send point; the
// evaluator runs it on a worker thread and the send is a plain message send. The host's refusal line and
// the requester's `World-time broadcast` answer are the run's evidence.
((System.Func<string>)(() => {
	var name = {{s:speed}};
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	var control = services == null ? null : services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.IWorldTimeControl));
	if (control == null) { return "{\"ok\":false,\"error\":\"no-world-time-control\"}"; }
	var speed = CasualtiesUnknownOnline.Runtime.Protocol.WorldTimeSpeed.Normal;
	if (name == "normal") { speed = CasualtiesUnknownOnline.Runtime.Protocol.WorldTimeSpeed.Normal; }
	else if (name == "fast") { speed = CasualtiesUnknownOnline.Runtime.Protocol.WorldTimeSpeed.Fast; }
	else if (name == "superfast") { speed = CasualtiesUnknownOnline.Runtime.Protocol.WorldTimeSpeed.SuperFast; }
	else if (name == "unconsciousfast") { speed = CasualtiesUnknownOnline.Runtime.Protocol.WorldTimeSpeed.UnconsciousFast; }
	else if (name == "dyingfast") { speed = CasualtiesUnknownOnline.Runtime.Protocol.WorldTimeSpeed.DyingFast; }
	else { return "{\"ok\":false,\"error\":\"unknown-speed\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService;
	var presence = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.GameAdapter.IWorldPresenceQuery)) as CasualtiesUnknownOnline.Runtime.GameAdapter.IWorldPresenceQuery;
	var gate = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.GameAdapter.IStartGateState)) as CasualtiesUnknownOnline.Runtime.GameAdapter.IStartGateState;
	var role = session == null ? "" : System.Convert.ToString(session.Role);
	var active = session != null && session.SessionActive;
	var sent = role == "Guest" && active;
	if (sent) { ((CasualtiesUnknownOnline.Runtime.Session.World.IWorldTimeControl)control).SendRequest(speed); }
	var camera = PlayerCamera.main;
	return "{\"ok\":true"
		+ ",\"requested\":\"" + name + "\""
		+ ",\"sent\":" + (sent ? "true" : "false")
		+ ",\"role\":\"" + role + "\""
		+ ",\"sessionActive\":" + (active ? "true" : "false")
		+ ",\"inWorld\":" + (presence != null && presence.IsInWorldOrGenerating ? "true" : "false")
		+ ",\"gateWaiting\":" + (gate != null && gate.IsWaitingForReady ? "true" : "false")
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"curTimeScale\":\"" + (camera == null ? "none" : System.Convert.ToString(camera.curTimeScale)) + "\"}";
}))()
