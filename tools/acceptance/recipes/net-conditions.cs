// recipe: net-conditions
// args: mode=s latencyMs=n jitterMs=n lossPercent=n
// serves: the network-conditions family (a reliable round trip under latency and loss, the state
//         stream's self-healing, the deferral paths that run while the link is slow)
// returns: ok, enabled, latencyMs, jitterMs, lossPercent, error
//
// Diagnostics only. Shapes THIS client's IP-direct transport: every outbound payload waits
// `latencyMs` (+/- `jitterMs`), and `lossPercent` of the UNRELIABLE payloads are dropped. Reliable
// payloads are never dropped, and per-peer order is kept. `read` answers the profile in force; `clear`
// turns it off. The profile is never part of the wire and never persisted, so a case must state it in
// its verdict: "the item landed at 150 ms and 20% loss" is a different claim from "it landed".
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var control = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Networking.INetConditionsControl)) as CasualtiesUnknownOnline.Runtime.Networking.INetConditionsControl;
	if (control == null) { return "{\"ok\":false,\"error\":\"no-control\"}"; }
	var mode = {{s:mode}};
	if (mode == "set") {
		control.Set(new CasualtiesUnknownOnline.Runtime.Networking.NetConditionsProfile(true, (int)({{n:latencyMs}}), (int)({{n:jitterMs}}), (int)({{n:lossPercent}})));
	} else if (mode == "clear") {
		control.Clear();
	}
	var profile = control.Current;
	return "{\"ok\":true,\"enabled\":" + (profile.Enabled ? "true" : "false") + ",\"latencyMs\":" + profile.LatencyMs + ",\"jitterMs\":" + profile.JitterMs + ",\"lossPercent\":" + profile.LossPercent + "}";
}))()
