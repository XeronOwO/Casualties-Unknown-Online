// recipe: blood-sample
// args: frames=n
// returns: ok, samples, min, max, first, last, series, error
//
// Samples the body the WOUND VIEW is drawing (`WoundView.view.body.bloodVolume`) once and answers it. It exists
// because a jitter a player reports as "+/-0.01 on the blood line" has to be measured, not guessed: the value has one writer per
// second (the authoritative snapshot) and, if it moves between them, a second one nobody has named yet (user report 2026-11-02:
// the remote medical panel's blood volume oscillated during an amputation). One eval is one frame; a caller that wants a series
// must issue separate evals, never sleep on the game's main thread (acceptance rule, 2026-11-15).
((System.Func<string>)(() => {
	var frames = (int){{n:frames}};
	var view = WoundView.view;
	if (view == null || view.body == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; }
	var value = view.body.bloodVolume;
	var text = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
	return "{\"ok\":true,\"samples\":1,\"framesRequested\":" + frames.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"min\":" + text + ",\"max\":" + text + ",\"first\":" + text + ",\"last\":" + text
		+ ",\"series\":\"" + text + "\"}";
}))()