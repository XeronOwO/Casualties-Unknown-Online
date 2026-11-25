// recipe: visibility-read
// args: target=s
// an empty target reads the one remote member.
// serves: host-rule-toggles, medical-shrapnel, the acceptance rollback that puts the pair back together
// returns: ok, target, clear, error, detail
//
// Asks the product's OWN visibility oracle the question its gates ask before a push, a carry, a remote medical session
// or a shrapnel break: `IPlayerInteractionVisibility.HasLineOfSight(local, target)` — a Ground-layer linecast between
// the two players' world positions (src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionVisibility.cs). A row that
// has just placed a body reads this instead of guessing from the distance: standing 1.5 units away on the far side of a
// wall still reads "no-line-of-sight" and the gesture never leaves the client (measured 2026-11-25: leave the pair out
// of sight and both the push and the remote shrapnel session are refused with exactly that answer).
((System.Func<string>)(() => {
	var target = {{s:target}};
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl));
	var entities = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.EntitySync.IEntitySyncControl));
	var visibility = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionVisibility));
	if (session == null || entities == null || visibility == null)
	{
		return "{\"ok\":false,\"error\":\"no-visibility-service\"}";
	}
	var control = (CasualtiesUnknownOnline.Runtime.Session.ISessionControl)session;
	var sync = (CasualtiesUnknownOnline.Runtime.Session.EntitySync.IEntitySyncControl)entities;
	var oracle = (CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionVisibility)visibility;
	ulong targetId;
	if (target.Length == 0)
	{
		var remotes = sync.RemotePlayers;
		if (remotes.Count == 0) { return "{\"ok\":false,\"error\":\"no-remote-player\"}"; }
		targetId = remotes[0].SteamId;
	}
	else if (!ulong.TryParse(target, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out targetId))
	{
		return "{\"ok\":false,\"error\":\"bad-target\",\"detail\":\"" + target.Replace("\"", "''") + "\"}";
	}
	var clear = oracle.HasLineOfSight(control.LocalSteamId, targetId);
	return "{\"ok\":true,\"target\":" + targetId
		+ ",\"clear\":" + (clear ? "true" : "false")
		+ ",\"local\":" + control.LocalSteamId + "}";
}))()
