// recipe: ui-fixture-seed
// args: none
// serves: save-native-character-field-parity (rows 1-3: the three game UI displays)
// returns: ok, versionText, bodyStatText, cInfo, calories, window, error, detail
//
// Stages the three native character fields the ticket's rows 1-3 display, so the values a continue
// restores are distinctive instead of random: the wound window's identity header (through the game's
// own `WoundView.SetCharDetails`, the same entry `SaveSystem` uses), the death screen's calorie
// counter (`PlayerCamera.caloriesConsumed`) and the ten-value happiness window (`Body.lastHappiness`).
// Staging through the native setters, not through the save file: the reading under test is the
// restore, not how the live values got there.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-local-camera\"}"; }
	var view = WoundView.view;
	if (view == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	view.SetCharDetails(180, 42, 777, 9);
	camera.caloriesConsumed = 4321;
	body.lastHappiness = new float[] { -50f, -45f, -40f, -35f, -30f, -25f, -20f, -15f, -10f, -5f };
	var window = new System.Text.StringBuilder();
	for (var i = 0; i < body.lastHappiness.Length; i++) {
		if (i > 0) { window.Append(","); }
		window.Append(body.lastHappiness[i].ToString("0.#", inv));
	}
	return "{\"ok\":true"
		+ ",\"versionText\":\"" + view.versionText.text.Replace('"', '\'').Replace('\\', '/')
		+ "\",\"bodyStatText\":\"" + view.bodyStatText.text.Replace('"', '\'').Replace('\\', '/')
		+ "\",\"cInfo\":\"" + string.Join(",", view.cInfo)
		+ "\",\"calories\":" + camera.caloriesConsumed.ToString(inv)
		+ ",\"window\":\"" + window.ToString() + "\"}";
}))()
