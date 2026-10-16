// recipe: member-id
// args: probe=s
// serves: member-report-bounds A (the forgery rows need the SteamId of the member whose identity is stolen)
// returns: ok, probe, localSteamId, hostId, role, sessionActive, error, detail
//
// The smallest possible read of "who am I": one client answers with its OWN SteamId, its host's and its
// role. A recipe cannot DERIVE another member's identity here — the evaluator's Mono REPL resolves types
// against the loaded assemblies only, and nothing in this tree hands out the presence table to it — so
// the run asks the member itself, which is also the only source with no guessing in it. `probe` is
// echoed back so a run can prove the argument substitution reached this client. One eval.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var probe = {{s:probe}};
	return "{\"ok\":true"
		+ ",\"probe\":\"" + probe + "\""
		+ ",\"localSteamId\":\"" + session.LocalSteamId.ToString(inv) + "\""
		+ ",\"hostId\":\"" + session.HostSteamId.ToString(inv) + "\""
		+ ",\"role\":\"" + session.Role.ToString() + "\""
		+ ",\"sessionActive\":" + (session.SessionActive ? "true" : "false") + "}";
}))()
