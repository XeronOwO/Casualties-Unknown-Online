// recipe: scene-repeat
// args: none
// serves: enemy-snapshot-binding-recovery row 2 (the member's own readiness-window repeat report; the host's entry-repair carrier for one repair group)
// returns: ok, utc, sent, role, error, detail
//
// Re-sends THIS client's last absolute scene report through the product's own readiness-window seam
// (ISessionControl.ResendSceneState — the call SessionControlConvergence makes while an entry window is
// still re-asserting). On a member the host answers the repeat with its entry-repair group, which in a
// world/layer generation window carries no enemy snapshot; on the host the call re-reports the host's own
// state. The run uses it to put one real repair group on the wire at a chosen moment; the member's
// `Scene state re-reported: …` line and the host's `Repeat scene report from …` line are the product's own
// account of it. One eval.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	var sent = false;
	var detail = "";
	try { sent = session.ResendSceneState(); }
	catch (System.Exception e) { detail = e.GetType().Name + ": " + e.Message; }
	if (detail.Length != 0) { return "{\"ok\":false,\"error\":\"resend-failed\",\"detail\":\"" + detail + "\"}"; }
	return "{\"ok\":true,\"utc\":\"" + System.DateTime.UtcNow.ToString("o") + "\",\"sent\":" + (sent ? "true" : "false")
		+ ",\"role\":\"" + session.Role.ToString() + "\"}";
}))()
