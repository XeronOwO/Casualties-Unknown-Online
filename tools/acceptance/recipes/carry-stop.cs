// recipe: carry-stop
// args: target=s
// serves: carry-piggyback-rider-position-smoothing, carry-piggyback-vertical-placement-asymmetry,
//         carrier-sit-while-carrying, carried-player-idle-sit-suppression, carried-unconscious-body-simulation
// returns: ok, carried, requester
//
// Releases the carry relation through the Online UI's own release entry
// (IPlayerInteractionControl.SendCarryStopRequest). Either half may release: the caller carrying someone
// resolves that player, a caller who is being carried releases itself. target=auto does that; an explicit
// decimal SteamId is the carried player to release, for a caller whose local mirror has not caught up yet.
((System.Func<string>)(() => {
	var target = {{s:target}};
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService;
	var control = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl)) as CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl;
	if (session == null || control == null) { return "{\"ok\":false,\"error\":\"no-player-interaction\"}"; }
	if (!session.SessionActive || !session.LocalInWorld) { return "{\"ok\":false,\"error\":\"not-in-world\"}"; }
	var carried = 0UL;
	var localCarried = 0UL;
	var localCarrier = 0UL;
	if (control.TryGetCarried(session.LocalSteamId, out localCarried)) { carried = localCarried; }
	else if (control.TryGetCarrier(session.LocalSteamId, out localCarrier)) { carried = session.LocalSteamId; }
	else if (target != "auto") { ulong.TryParse(target, out carried); }
	if (carried == 0UL) { return "{\"ok\":false,\"error\":\"no-relation\"}"; }
	control.SendCarryStopRequest(carried);
	return "{\"ok\":true,\"carried\":\"" + carried + "\",\"requester\":\"" + session.LocalSteamId + "\"}";
}))()
