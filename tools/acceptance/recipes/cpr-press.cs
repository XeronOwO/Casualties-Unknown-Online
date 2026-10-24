// recipe: cpr-press
// args: mode=s
// serves: remote-medical-cpr (the wound view's CPR control rows)
// returns: ok, mode, found, active, interactable, pressed, error, detail
//
// Reads or presses the wound view's CPR control — the clone CprButtonCoordinator places beside the panel's own
// `specialUseButton`. mode=read reports whether the control exists, is active and is interactable; mode=press invokes
// its `onClick`, which IS the click a player makes (the button's listener is the coordinator's handler), so the row is
// judged from the panel's own object rather than from a pixel.
//
// The control is found among the template button's siblings: the panel holds one instance and its parent is the layout
// the clone was placed into.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	if (mode != "read" && mode != "press") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be read or press\"}"; }
	var view = WoundView.view;
	if (view == null || view.specialUseButton == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; }
	var parent = view.specialUseButton.transform.parent;
	var found = false;
	var active = false;
	var interactable = false;
	var pressed = false;
	for (var i = 0; i < parent.childCount; i++) {
		var child = parent.GetChild(i);
		if (child.gameObject.name != "CUO CPR Control") { continue; }
		found = true;
		active = child.gameObject.activeInHierarchy;
		var button = child.GetComponent<UnityEngine.UI.Button>();
		if (button != null) {
			interactable = button.interactable;
			if (mode == "press" && active && button.interactable) {
				button.onClick.Invoke();
				pressed = true;
			}
		}
		break;
	}
	return "{\"ok\":true"
		+ ",\"mode\":\"" + mode + "\""
		+ ",\"found\":" + (found ? "true" : "false")
		+ ",\"active\":" + (active ? "true" : "false")
		+ ",\"interactable\":" + (interactable ? "true" : "false")
		+ ",\"pressed\":" + (pressed ? "true" : "false") + "}";
}))()
