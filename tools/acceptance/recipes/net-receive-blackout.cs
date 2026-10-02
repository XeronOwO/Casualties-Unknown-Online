// recipe: net-receive-blackout
// args: mode=s
// serves: guest-block-mutation-re-report rows 1-4 and 10, the swallowed-guest-report family
// returns: ok, mode, armed, subscribers, subscribersAfter, parked, error, detail
//
// Arms or disarms a short INBOUND blackout on the LOCAL client. The client keeps polling and keeps
// SENDING; only the dispatch of every frame it receives stops, because the router's field-like
// MessageReceived event is temporarily emptied and its subscribers are parked in this process's
// AppDomain data (one invocation is one fresh evaluation, so arm and disarm are two calls). What the
// other side sees is the production lazy-P2P swallow: its transport reported the send as successful
// and this side never got the frame. PacketReceiver is the event's only subscriber, so the blackout
// cuts exactly the packet plane's inbound dispatch. The window is a blunt instrument — it drops EVERY
// inbound frame, not only the one the scenario is about, and it must stay short and single-stepped.
// mode=status reports the current subscriber count and whether a delegate is parked.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var router = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Networking.CuoNetworkRouter));
	if (router == null) { return "{\"ok\":false,\"error\":\"no-router\"}"; }
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const string key = "cuo.acceptance.receive-blackout";
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var field = router.GetType().GetField("MessageReceived", flags);
	if (field == null) { return "{\"ok\":false,\"error\":\"no-event-field\"}"; }
	var current = field.GetValue(router) as System.Action<ulong, byte[]>;
	var parked = System.AppDomain.CurrentDomain.GetData(key) as System.Action<ulong, byte[]>;
	var subscribers = current == null ? 0 : current.GetInvocationList().Length;
	if (mode == "status") {
		return "{\"ok\":true,\"mode\":\"status\",\"armed\":" + (current == null ? "true" : "false")
			+ ",\"subscribers\":" + subscribers.ToString(inv)
			+ ",\"subscribersAfter\":" + subscribers.ToString(inv)
			+ ",\"parked\":" + (parked == null ? "false" : "true") + "}";
	}
	if (mode == "on") {
		if (current == null) { return "{\"ok\":false,\"error\":\"already-armed\"}"; }
		System.AppDomain.CurrentDomain.SetData(key, current);
		field.SetValue(router, null);
		return "{\"ok\":true,\"mode\":\"on\",\"armed\":true"
			+ ",\"subscribers\":" + subscribers.ToString(inv)
			+ ",\"subscribersAfter\":0,\"parked\":true}";
	}
	if (mode == "off") {
		if (parked == null) { return "{\"ok\":false,\"error\":\"not-armed\"}"; }
		field.SetValue(router, parked);
		System.AppDomain.CurrentDomain.SetData(key, null);
		return "{\"ok\":true,\"mode\":\"off\",\"armed\":false"
			+ ",\"subscribers\":" + subscribers.ToString(inv)
			+ ",\"subscribersAfter\":" + parked.GetInvocationList().Length.ToString(inv)
			+ ",\"parked\":false}";
	}
	return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be on, off or status\"}";
}))()
