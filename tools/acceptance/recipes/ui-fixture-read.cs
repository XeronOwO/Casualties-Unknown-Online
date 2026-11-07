// recipe: ui-fixture-read
// args: none
// serves: save-native-character-field-parity (rows 1-3: the three game UI displays)
// returns: ok, versionText, bodyStatText, cInfo, firmwareVer, specimenId, happyText, calories, window, deathStatsText, deathStatsChildren, error, detail
//
// Reads back, on the client that owns the screen, the three game displays the ticket's rows 1-3 name:
// the wound window's identity header (`versionText` / `bodyStatText` / `cInfo`, written by the game's
// own `SetCharDetails`), the death screen's calorie counter (`deathStats` child 11, written by
// `PlayerCamera` at death) and the ten-value happiness window (`Body.lastHappiness`) plus the live
// total the window feeds (`happyText`). The death text is empty until the death screen writes it,
// which is why the caller reads this again after the character dies.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-local-camera\"}"; }
	var view = WoundView.view;
	if (view == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var version = view.versionText == null ? "" : view.versionText.text;
	var bodyStat = view.bodyStatText == null ? "" : view.bodyStatText.text;
	var happy = view.happyText == null ? "" : view.happyText.text;
	var window = new System.Text.StringBuilder();
	if (body.lastHappiness != null) {
		for (var i = 0; i < body.lastHappiness.Length; i++) {
			if (i > 0) { window.Append(","); }
			window.Append(body.lastHappiness[i].ToString("0.#", inv));
		}
	}
	var deathText = "";
	var deathChildren = -1;
	if (camera.deathStats != null) {
		deathChildren = camera.deathStats.childCount;
		if (deathChildren > 11) {
			var child = camera.deathStats.GetChild(11);
			var label = child == null ? null : child.GetComponent<TMPro.TextMeshProUGUI>();
			deathText = label == null ? "" : label.text;
		}
	}
	return "{\"ok\":true"
		+ ",\"versionText\":\"" + version.Replace('"', '\'').Replace('\\', '/')
		+ "\",\"bodyStatText\":\"" + bodyStat.Replace('"', '\'').Replace('\\', '/')
		+ "\",\"cInfo\":\"" + string.Join(",", view.cInfo)
		+ "\",\"firmwareVer\":\"" + (WoundView.firmwareVer ?? "null").Replace('"', '\'')
		+ "\",\"specimenId\":" + WoundView.specimenId.ToString(inv)
		+ ",\"happyText\":\"" + happy.Replace('"', '\'').Replace('\\', '/')
		+ "\",\"calories\":" + camera.caloriesConsumed.ToString(inv)
		+ ",\"window\":\"" + window.ToString() + "\""
		+ ",\"deathStatsText\":\"" + deathText.Replace('"', '\'').Replace('\\', '/') + "\""
		+ ",\"deathStatsChildren\":" + deathChildren.ToString(inv) + "}";
}))()
