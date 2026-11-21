// recipe: kick-member
// args: steamId=s reason=s
// serves: one-acceptance-run-covers-every-case
// returns: ok, kicked, steamId, error
//
// Removes one member from the host's session so a case starts from the shape it declares: the batch's
// model is one guest in the run, and a member row left behind by an earlier case (or by a second client
// that is still connected) makes rows that address "the member" answer with two ids and fail on their own
// precondition (measured 2026-11-21: `the run does not have exactly one member: 2` in the full run).
// Host only; the removal is the product's own `KickMember`, not a driver-side edit of the roster.
((System.Func<string>)(() => {
	var steamId = System.UInt64.Parse({{s:steamId}}, System.Globalization.CultureInfo.InvariantCulture);
	var reason = {{s:reason}};
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var service = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService));
	if (service == null) { return "{\"ok\":false,\"error\":\"no-session-service\"}"; }
	var kicked = ((CasualtiesUnknownOnline.Runtime.Session.SessionService)service).KickMember(steamId, reason);
	return "{\"ok\":true,\"kicked\":" + (kicked ? "true" : "false") + ",\"steamId\":\"" + steamId + "\"}";
}))()
