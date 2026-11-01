// recipe: blood-sample
// args: frames=n
// returns: ok, samples, min, max, first, last, series, error
//
// Samples the body the WOUND VIEW is drawing (`WoundView.view.body.bloodVolume`) once per frame and answers the series. It exists
// because a jitter a player reports as "+/-0.01 on the blood line" has to be measured, not guessed: the value has one writer per
// second (the authoritative snapshot) and, if it moves between them, a second one nobody has named yet (user report 2026-11-02:
// the remote medical panel's blood volume oscillated during an amputation).
((System.Func<string>)(() => {
	var frames = (int){{n:frames}};
	var view = WoundView.view;
	if (view == null || view.body == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; }
	var start = UnityEngine.Time.frameCount;
	var samples = 0;
	var min = float.MaxValue;
	var max = float.MinValue;
	var first = 0f;
	var last = 0f;
	var series = "";
	while (UnityEngine.Time.frameCount - start < frames) {
		var value = view.body.bloodVolume;
		if (samples == 0) { first = value; }
		last = value;
		if (value < min) { min = value; }
		if (value > max) { max = value; }
		if (samples < 24) { series += value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + (samples == 23 ? "" : "|"); }
		samples++;
		System.Threading.Thread.Sleep(1);
	}

	return "{\"ok\":true,\"samples\":" + samples.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"min\":" + min.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"max\":" + max.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"first\":" + first.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"last\":" + last.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"series\":\"" + series + "\"}";
}))()