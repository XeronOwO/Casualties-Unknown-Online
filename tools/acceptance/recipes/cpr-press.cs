// recipe: cpr-press
// args: mode=s control=s
// serves: remote-medical-cpr (the wound view's CPR control rows: both channels)
// returns: ok, mode, control, found, active, interactable, pressed, error, detail
//
// Reads or presses one of the wound view's two CPR controls, the clones CprButtonCoordinator places beside the panel's
// own `specialUseButton`: `control=compress` is the compressions control (and the start, before a session runs),
// `control=breath` the ventilation one. mode=read reports whether the control exists, is active and is interactable;
// mode=press invokes its `onClick`, which IS the click a player makes, so the row is judged from the panel's own object
// rather than from a pixel.
//
// The control is found among the template button's siblings: the panel holds one instance of each and their parent is
// the layout the clones were placed into.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var control = {{s:control}};
	if (mode != "read" && mode != "press") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be read or press\"}"; }
	if (control != "compress" && control != "breath") { return "{\"ok\":false,\"error\":\"bad-control\",\"detail\":\"control must be compress or breath\"}"; }
	var wanted = control == "compress" ? "CUO CPR Control" : "CUO CPR Breath";
	var view = WoundView.view;
	if (view == null || view.specialUseButton == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; }
	var parent = view.specialUseButton.transform.parent;
	var found = false;
	var active = false;
	var interactable = false;
	var pressed = false;
	for (var i = 0; i < parent.childCount; i++) {
		var child = parent.GetChild(i);
		if (child.gameObject.name != wanted) { continue; }
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
		+ ",\"control\":\"" + control + "\""
		+ ",\"found\":" + (found ? "true" : "false")
		+ ",\"active\":" + (active ? "true" : "false")
		+ ",\"interactable\":" + (interactable ? "true" : "false")
		+ ",\"pressed\":" + (pressed ? "true" : "false") + "}";
}))()
