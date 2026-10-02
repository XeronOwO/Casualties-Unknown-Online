// recipe: generation-read
// args: none
// serves: world-layer-generation-identity rows 2, 5 and 7, generation-identity-remaining-families rows 2, 5 and 6
// returns: ok, role, inWorld, localSteamId, hostSteamId, runEpoch, layerIndex, error, detail
//
// Reads THIS client's own world-report generation — the kernel run baseline (RunEpoch, LayerIndex) every
// direct report of the stamped families is stamped with — plus the role and in-world facts a verdict
// brackets its scenario with. runEpoch/layerIndex are null when this side holds no committed run
// baseline, the same condition that makes its reports travel unstamped. Read-only, one eval.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	var authority = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.ItemKernelAuthority));
	if (authority == null) { return "{\"ok\":false,\"error\":\"no-kernel-authority\"}"; }
	var hasEpoch = false;
	var epoch = 0UL;
	var epochProperty = authority.GetType().GetProperty("CurrentRunEpoch", flags);
	if (epochProperty != null) {
		var epochValue = epochProperty.GetValue(authority, null);
		if (epochValue != null) {
			var valueProperty = epochValue.GetType().GetProperty("Value", flags);
			if (valueProperty != null) { epoch = System.Convert.ToUInt64(valueProperty.GetValue(epochValue, null), inv); hasEpoch = true; }
		}
	}
	var hasLayer = false;
	var layer = -1;
	var queryRun = authority.GetType().GetMethod("QueryRun", flags, null, new System.Type[0], null);
	if (queryRun != null) {
		var run = queryRun.Invoke(authority, null);
		if (run != null) {
			var layerProperty = run.GetType().GetProperty("LayerIndex", flags);
			if (layerProperty != null) { layer = System.Convert.ToInt32(layerProperty.GetValue(run, null), inv); hasLayer = true; }
		}
	}
	return "{\"ok\":true,\"role\":\"" + session.Role.ToString() + "\""
		+ ",\"inWorld\":" + (session.LocalInWorld ? "true" : "false")
		+ ",\"localSteamId\":\"" + session.LocalSteamId.ToString(inv) + "\""
		+ ",\"hostSteamId\":\"" + session.HostSteamId.ToString(inv) + "\""
		+ ",\"runEpoch\":" + (hasEpoch ? epoch.ToString(inv) : "null")
		+ ",\"layerIndex\":" + (hasLayer ? layer.ToString(inv) : "null") + "}";
}))()
