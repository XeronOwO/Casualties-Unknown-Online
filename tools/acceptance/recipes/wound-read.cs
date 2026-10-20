// recipe: wound-read
// args: toggle=s
// serves: save-native-character-field-parity, save-layer-time-not-carried, remote-medical-panel-header-shows-viewer-identity
// returns: ok, active, bodyStatText, versionText, cInfo, happyText, nameText, timeTextBottom, timeTextBar, error, detail
//
// Opens the game's own wound view (PlayerCamera.ToggleWoundView, the native button's own path) and
// reads the LIVE text components the player would see, so a restored character's native fields are
// judged from the game's own surface. `toggle` is on, off or keep.
//
// The identity header is read as the four ints the native SetCharDetails wrote (`cInfo`) plus the
// two labels it fills: the remote medical focus borrows exactly those, and its rows are judged on
// the values rather than on a picture of them.
((System.Func<string>)(() => {
	var toggle = {{s:toggle}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-local-camera\"}"; }
	var view = WoundView.view;
	if (view == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; }
	if (toggle == "on" && !camera.woundView.activeSelf) { camera.ToggleWoundView(false); }
	if (toggle == "off" && camera.woundView.activeSelf) { camera.ToggleWoundView(false); }
	var bodyStat = view.bodyStatText == null ? "" : view.bodyStatText.text;
	var version = view.versionText == null ? "" : view.versionText.text;
	var happy = view.happyText == null ? "" : view.happyText.text;
	var name = view.nameText == null ? "" : view.nameText.text;
	var bottom = view.timeTextBottom == null ? "" : view.timeTextBottom.text;
	var bar = view.timeTextBar == null ? "" : view.timeTextBar.text;
	var info = view.cInfo == null ? "" : string.Join(",", view.cInfo);
	return "{\"ok\":true,\"active\":" + (camera.woundView.activeSelf ? "true" : "false")
		+ ",\"bodyStatText\":\"" + bodyStat.Replace('"', '\'').Replace('\\', '/')
		+ "\",\"versionText\":\"" + version.Replace('"', '\'').Replace('\\', '/')
		+ "\",\"cInfo\":\"" + info
		+ "\",\"happyText\":\"" + happy.Replace('"', '\'').Replace('\\', '/')
		+ "\",\"nameText\":\"" + name.Replace('"', '\'').Replace('\\', '/')
		+ "\",\"timeTextBottom\":\"" + bottom.Replace('"', '\'').Replace('\\', '/')
		+ "\",\"timeTextBar\":\"" + bar.Replace('"', '\'').Replace('\\', '/') + "\"}";
}))()
