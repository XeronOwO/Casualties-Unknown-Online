// recipe: host-rules-read
// args: none
// serves: host-rules-replication
// returns: ok, role, sessionActive, announced, allowRemoteInventoryTake, allowMemberItemDataWrite,
//          piggybackWeightMultiplier, nativeBindingParity, permadeath, reviveFromTrader,
//          forbidsMemberItemDataWrite, error
//
// Reads the host-rule surface THIS client is acting on, so an acceptance record can cite what the client
// itself believed rather than what its log said: whether an announcement has arrived at all, the values in
// force (`HostRulesService.Current` — the host's announced set on a member, this process's own config on a
// host), and the local-check verdict the member's own console asks before it writes
// (`AnnouncedHostRules.Forbids`). One eval, read-only.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl));
	var store = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.HostRules.AnnouncedHostRules));
	var service = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.HostRules.HostRulesService));
	if (session == null || store == null || service == null) { return "{\"ok\":false,\"error\":\"no-rule-services\"}"; }
	var control = (CasualtiesUnknownOnline.Runtime.Session.ISessionControl)session;
	var announced = (CasualtiesUnknownOnline.Runtime.Session.HostRules.AnnouncedHostRules)store;
	var inForce = ((CasualtiesUnknownOnline.Runtime.Session.HostRules.HostRulesService)service).Current;
	var builder = new System.Text.StringBuilder();
	builder.Append("{\"ok\":true")
		.Append(",\"role\":\"").Append(control.Role.ToString()).Append('"')
		.Append(",\"sessionActive\":").Append(control.SessionActive ? "true" : "false")
		.Append(",\"announced\":").Append(announced.Current.HasValue ? "true" : "false")
		.Append(",\"allowRemoteInventoryTake\":").Append(inForce.AllowRemoteInventoryTake ? "true" : "false")
		.Append(",\"allowMemberItemDataWrite\":").Append(inForce.AllowMemberItemDataWrite ? "true" : "false")
		.Append(",\"piggybackWeightMultiplier\":").Append(inForce.PiggybackWeightMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture))
		.Append(",\"nativeBindingParity\":\"").Append(inForce.NativeBindingParity.ToString()).Append('"')
		.Append(",\"permadeath\":").Append(inForce.Permadeath ? "true" : "false")
		.Append(",\"reviveFromTrader\":").Append(inForce.ReviveFromTrader ? "true" : "false")
		.Append(",\"forbidsMemberItemDataWrite\":").Append(announced.Forbids(CasualtiesUnknownOnline.Runtime.Session.HostRules.HostRuleId.AllowMemberItemDataWrite) ? "true" : "false")
		.Append('}');
	return builder.ToString();
}))()
