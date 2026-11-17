// recipe: push-player
// args: target=s
// serves: host-rule-toggles
// returns: ok, target, requester
//
// Enters the push path the Online UI's own Push row uses (IPlayerInteractionControl.SendPushRequest), so
// the rule gate, the line-of-sight/reach judgments and the host's validation all decide — nothing about
// the gesture is written by hand. The local visibility gate the runtime would silently drop the request
// on is checked first, so ok=true means the request left this client. target=auto picks the single other
// in-world member; pass a decimal SteamId when more than two players are in the world.
((System.Func<string>)(() => {
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
	var visibility = services.GetService(typeof(CasualtiesUnknownOnline.GameAdapter.PlayerInteractionVisibility)) as CasualtiesUnknownOnline.GameAdapter.PlayerInteractionVisibility;
	if (visibility != null && !visibility.HasLineOfSight(session.LocalSteamId, targetId)) {
		return "{\"ok\":false,\"error\":\"no-line-of-sight\",\"detail\":\"the local visibility gate would drop the request\"}";
	}
	control.SendPushRequest(targetId);
	return "{\"ok\":true,\"target\":\"" + targetId + "\",\"requester\":\"" + session.LocalSteamId + "\"}";
}))()
