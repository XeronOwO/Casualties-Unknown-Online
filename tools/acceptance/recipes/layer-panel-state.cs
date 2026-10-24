// recipe: layer-panel-state
// args: none
// serves: layer-end-save-and-exit-native-write (the host panel's visibility rows)
// returns: ok, halfHeight, floorY, bodyY, panelActive, panelField, error, detail
//
// Reads the HOST's end-of-layer panel as the game's own object state, beside the body position that decides the
// native floor test and the floor line the CUO gate computes from the world's own `halfHeight`
// (`-(float)halfHeight + 3.1`, the native condition). A run places bodies with `body-place` and reads this after, so
// "the panel appears only when the share of LIVING players at the floor reaches the host rule" is judged from the
// object, never from a screenshot.
//
// `panelField` names where the panel was found (the world's `savePanel`), because a null object and an inactive
// object are different readings: null means this build has no panel to gate.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var halfHeight = (float)world.halfHeight;
	var floorY = -halfHeight + 3.1f;
	var panel = world.savePanel;
	if (panel == null) { return "{\"ok\":true,\"halfHeight\":" + halfHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ",\"floorY\":" + floorY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ",\"bodyY\":" + body.transform.position.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ",\"panelActive\":false,\"panelField\":\"savePanel=null\"}"; }
	return "{\"ok\":true"
		+ ",\"halfHeight\":" + halfHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"floorY\":" + floorY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"bodyY\":" + body.transform.position.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"panelActive\":" + (panel.activeSelf ? "true" : "false")
		+ ",\"panelField\":\"savePanel\"}";
}))()
