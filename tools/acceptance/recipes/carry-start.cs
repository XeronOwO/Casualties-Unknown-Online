// recipe: carry-start
// args: mode=s target=s
// serves: carry-piggyback-rider-position-smoothing, carry-piggyback-vertical-placement-asymmetry,
//         carrier-sit-while-carrying, carried-player-idle-sit-suppression, carried-unconscious-body-simulation,
//         carried-inventory-registration-re-report, guest-container-contents-ghost-drops-on-host
// returns: ok, mode, target, requester
//
// Enters the carry path the Online UI's own carry buttons use
// (IPlayerInteractionControl.SendCarryStartRequest / SendPiggybackRequest / SendCarryOnBackRequest), so
// the host's visibility and health validation plus the kernel projection decide — no relation is written
// by hand. The local visibility gate the runtime would silently drop the request on is checked first, so
// ok=true means the request left this client. mode=carry carries a downed body (the target must be
// unconscious or dead, seen through the host's authoritative snapshot); mode=piggyback climbs onto the
// target's back; mode=onback takes the target onto the caller's back. target=auto picks the single other
// in-world member; pass a decimal SteamId when more than two players are in the world.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var target = {{s:target}};
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService;
	var control = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl)) as CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl;
	if (session == null || control == null) { return "{\"ok\":false,\"error\":\"no-player-interaction\"}"; }
	if (!session.SessionActive || !session.LocalInWorld) { return "{\"ok\":false,\"error\":\"not-in-world\"}"; }
	var targetId = 0UL;
	if (target == "auto") {
		foreach (var member in session.Members) {
			if (member.SteamId != session.LocalSteamId && member.InWorld) { targetId = member.SteamId; break; }
		}
	}
	else {
		ulong.TryParse(target, out targetId);
	}
	if (targetId == 0UL) { return "{\"ok\":false,\"error\":\"no-target\",\"detail\":\"no other in-world member and no explicit target\"}"; }
	if (mode != "carry" && mode != "piggyback" && mode != "onback") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be carry, piggyback or onback\"}"; }
	var visibility = services.GetService(typeof(CasualtiesUnknownOnline.GameAdapter.PlayerInteractionVisibility)) as CasualtiesUnknownOnline.GameAdapter.PlayerInteractionVisibility;
	if (visibility != null && !visibility.HasLineOfSight(session.LocalSteamId, targetId)) {
		return "{\"ok\":false,\"error\":\"no-line-of-sight\",\"detail\":\"the local visibility gate would drop the request\"}";
	}
	if (mode == "carry") { control.SendCarryStartRequest(targetId); }
	else if (mode == "piggyback") { control.SendPiggybackRequest(targetId); }
	else { control.SendCarryOnBackRequest(targetId); }
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"target\":\"" + targetId + "\",\"requester\":\"" + session.LocalSteamId + "\"}";
}))()
