// recipe: screen-size
// args: none
// returns: ok, width, height, centerX, centerY, error, detail
//
// The staged pointer lives in the game's screen pixel space (bottom-left origin), so a fixture that
// moves the hand around the centre of the screen needs the current size rather than a hardcoded one:
// the window is resizable and a hardcoded centre puts the gesture off the minigame's ring.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var width = UnityEngine.Screen.width;
	var height = UnityEngine.Screen.height;
	return "{\"ok\":true,\"width\":" + width.ToString(inv)
		+ ",\"height\":" + height.ToString(inv)
		+ ",\"centerX\":" + (width / 2f).ToString("0.###", inv)
		+ ",\"centerY\":" + (height / 2f).ToString("0.###", inv) + "}";
}))()
