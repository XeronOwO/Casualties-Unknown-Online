// recipe: screen-project
// args: x=n y=n
// serves: remote-medical-cpr (the wound view's CPR control rows)
// returns: ok, worldX, worldY, screenX, screenY, screenHeight, controlFound, controlScreenX, controlScreenY, controlActive,
//          error, detail
//
// Projects one world point to screen pixels through the game's own camera, and — while a remote wound view is open —
// reports where the CPR control sits on screen and whether it is active. A run clicks the returned coordinates with
// `tools/acceptance/send-input.ps1` (Windows' origin is top-left: yWin = screenHeight - screenY), which is how a gesture
// the in-process driver has no control id for is performed without guessing at pixel positions.
//
// Every type here is spelled with its namespace, the way `cuo-ui-dump` spells its own: the evaluator's compile unit
// resolves fully qualified names reliably.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var cam = UnityEngine.Camera.main;
	if (cam == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var worldX = {{n:x}};
	var worldY = {{n:y}};
	var screen = cam.WorldToScreenPoint(new UnityEngine.Vector3(worldX, worldY, 0f));
	var found = false;
	var controlX = 0f;
	var controlY = 0f;
	var controlActive = false;
	var view = WoundView.view;
	if (view != null && view.specialUseButton != null) {
		var parent = view.specialUseButton.transform.parent;
		for (var i = 0; i < parent.childCount; i++) {
			var child = parent.GetChild(i);
			if (child.gameObject.name != "CUO CPR Control") { continue; }
			var rect = child.GetComponent<UnityEngine.RectTransform>();
			if (rect == null) { continue; }
			found = true;
			controlActive = child.gameObject.activeInHierarchy;
			var center = rect.TransformPoint(rect.rect.center);
			var controlScreen = cam.WorldToScreenPoint(center);
			controlX = controlScreen.x;
			controlY = controlScreen.y;
			break;
		}
	}
	return "{\"ok\":true"
		+ ",\"worldX\":" + worldX.ToString("0.###", inv)
		+ ",\"worldY\":" + worldY.ToString("0.###", inv)
		+ ",\"screenX\":" + screen.x.ToString("0.#", inv)
		+ ",\"screenY\":" + screen.y.ToString("0.#", inv)
		+ ",\"screenHeight\":" + cam.pixelHeight.ToString(inv)
		+ ",\"controlFound\":" + (found ? "true" : "false")
		+ ",\"controlScreenX\":" + controlX.ToString("0.#", inv)
		+ ",\"controlScreenY\":" + controlY.ToString("0.#", inv)
		+ ",\"controlActive\":" + (controlActive ? "true" : "false") + "}";
}))()
