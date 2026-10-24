// recipe: cpr-press
// args: mode=s control=s count=n
// serves: remote-medical-cpr (the wound view's CPR control rows: both channels, and the rhythm band's two edges)
// returns: ok, mode, control, count, found, active, interactable, pressed, error, detail
//
// Reads or presses one of the wound view's two CPR controls, the clones CprButtonCoordinator places beside the panel's
// own `specialUseButton`: `control=compress` is the compressions control (and the start, before a session runs),
// `control=breath` the ventilation one. mode=read reports whether the control exists, is active and is interactable;
// mode=press invokes its `onClick` — which IS the click a player makes — `count` times in a row, so the row is judged
// from the panel's own object rather than from a pixel.
//
// `count` above 1 dispatches the presses in the SAME frame: the control measures its interval from real time, so that is
// the rhythm band's FAST edge (a shove, half gain and a rib) and how this batch reads it. The SLOW edge is read by
// single presses a driver round-trip apart, which are slower than 65 a minute and earn nothing.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var control = {{s:control}};
	var count = (int)({{n:count}});
	if (mode != "read" && mode != "press") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be read or press\"}"; }
	if (control != "compress" && control != "breath") { return "{\"ok\":false,\"error\":\"bad-control\",\"detail\":\"control must be compress or breath\"}"; }
	if (count < 1) { return "{\"ok\":false,\"error\":\"bad-count\",\"detail\":\"count must be at least 1\"}"; }
	var wanted = control == "compress" ? "CUO CPR Control" : "CUO CPR Breath";
	var view = WoundView.view;
	if (view == null || view.specialUseButton == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; }
	var parent = view.specialUseButton.transform.parent;
	var found = false;
	var active = false;
	var interactable = false;
	var pressed = 0;
	for (var i = 0; i < parent.childCount; i++) {
		var child = parent.GetChild(i);
		if (child.gameObject.name != wanted) { continue; }
		found = true;
		active = child.gameObject.activeInHierarchy;
		var button = child.GetComponent<UnityEngine.UI.Button>();
		if (button != null) {
			interactable = button.interactable;
			if (mode == "press" && active && button.interactable) {
				for (var n = 0; n < count; n++) {
					button.onClick.Invoke();
					pressed++;
				}
			}
		}
		break;
	}
	return "{\"ok\":true"
		+ ",\"mode\":\"" + mode + "\""
		+ ",\"control\":\"" + control + "\""
		+ ",\"count\":" + count.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"found\":" + (found ? "true" : "false")
		+ ",\"active\":" + (active ? "true" : "false")
		+ ",\"interactable\":" + (interactable ? "true" : "false")
		+ ",\"pressed\":" + pressed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
