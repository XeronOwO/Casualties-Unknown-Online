// recipe: layer-panel-state
// args: none
// serves: layer-end-save-and-exit-native-write (the host panel's visibility rows and the member's hidden choices)
// returns: ok, halfHeight, floorY, bodyY, panelActive, choices, choicesVisible, panelField, error, detail
//
// Reads the end-of-layer panel as the game's own object state: the panel's `activeSelf`, the body position that decides
// the native floor test, the floor line the CUO gate computes from the world's own `halfHeight`
// (`-(float)halfHeight + 3.1`), and the two choice buttons it finds by the same persistent call names the adapter's own
// hiding pass uses (`ContinueRun`, `SaveAndExit`). A run places bodies with `body-place` and reads this after, so both
// halves of the user's ruling — the host's panel follows the living share, and a member's own panel shows the choices
// to nobody — are judged from objects, never from a screenshot.
//
// `panelField` names where the panel was found; `choices` counts the two buttons under it and `choicesVisible` says
// whether any of them is active, which is the member's row.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var halfHeight = (float)world.halfHeight;
	var floorY = -halfHeight + 3.1f;
	var panel = world.savePanel;
	if (panel == null) { return "{\"ok\":true,\"halfHeight\":" + halfHeight.ToString("0.###", inv) + ",\"floorY\":" + floorY.ToString("0.###", inv) + ",\"bodyY\":" + body.transform.position.y.ToString("0.###", inv) + ",\"panelActive\":false,\"choices\":0,\"choicesVisible\":false,\"panelField\":\"savePanel=null\"}"; }
	var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
	var choices = 0;
	var visible = false;
	for (var i = 0; i < buttons.Length; i++) {
		var button = buttons[i];
		var events = button.onClick;
		var matches = false;
		for (var e = 0; e < events.GetPersistentEventCount(); e++) {
			var name = events.GetPersistentMethodName(e);
			if (name == "ContinueRun" || name == "SaveAndExit") { matches = true; break; }
		}
		if (!matches) { continue; }
		choices++;
		if (button.gameObject.activeInHierarchy) { visible = true; }
	}
	return "{\"ok\":true"
		+ ",\"halfHeight\":" + halfHeight.ToString("0.###", inv)
		+ ",\"floorY\":" + floorY.ToString("0.###", inv)
		+ ",\"bodyY\":" + body.transform.position.y.ToString("0.###", inv)
		+ ",\"panelActive\":" + (panel.activeSelf ? "true" : "false")
		+ ",\"choices\":" + choices.ToString(inv)
		+ ",\"choicesVisible\":" + (visible ? "true" : "false")
		+ ",\"panelField\":\"savePanel\"}";
}))()
