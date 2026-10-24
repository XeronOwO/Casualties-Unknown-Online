// recipe: medical-open
// args: steamId=s name=s
// serves: remote-medical-cpr (opening the remote wound view for its control rows)
// returns: ok, steamId, opened, nativeOpen, found, error, detail
//
// Opens the remote medical view for one member through the SAME call the Online UI makes when a player opens a member's
// chart (`IRemoteMedicalPresentation.OpenRemoteMedical`), so a run can reach the panel without a mouse gesture. This is a
// setup substitution, not a player entry: the player's own entry is the member row in the Online UI, and the row this
// serves is the panel's CPR control.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var adapter = services.GetService(typeof(CasualtiesUnknownOnline.GameAdapter.GameAdapter));
	if (adapter == null) { return "{\"ok\":false,\"error\":\"no-adapter\"}"; }
	var presentation = adapter as CasualtiesUnknownOnline.Runtime.GameAdapter.IRemoteMedicalPresentation;
	if (presentation == null) { return "{\"ok\":false,\"error\":\"no-medical-presentation\"}"; }
	var steamId = System.UInt64.Parse({{s:steamId}}, System.Globalization.CultureInfo.InvariantCulture);
	var opened = presentation.OpenRemoteMedical(steamId, {{s:name}});
	var view = WoundView.view;
	return "{\"ok\":true"
		+ ",\"steamId\":" + steamId.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"opened\":" + (opened ? "true" : "false")
		+ ",\"nativeOpen\":" + (view != null && view.gameObject.activeInHierarchy ? "true" : "false")
		+ ",\"found\":" + (view != null ? "true" : "false") + "}";
}))()
