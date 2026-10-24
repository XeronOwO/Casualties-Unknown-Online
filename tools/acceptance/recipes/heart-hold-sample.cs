// recipe: heart-hold-sample
// args: heartRate=n bloodOxygen=n bloodPressure=n consciousness=n samples=n
// serves: host-heartbeat-sound-truncated (the beeps under a HELD critical state with a high heart rate)
// returns: ok, held, samples, audio (comma-joined thump positions, ms), playing, panelOpen, error, detail
//
// Holds the local body's critical vitals and samples the per-beat thump BETWEEN frames: the main thread is stalled with
// short sleeps, so the game's own simulation cannot decay the state (and every iteration rewrites it anyway), while the
// audio system keeps playing on its own thread. The returned series is the thump clip's position in milliseconds at each
// sample: a position that falls back to zero before reaching the clip's length is the truncation, and its cadence is the
// restart rate.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var heartRate = {{n:heartRate}};
	var bloodOxygen = {{n:bloodOxygen}};
	var bloodPressure = {{n:bloodPressure}};
	var consciousness = {{n:consciousness}};
	var samples = (int)({{n:samples}});
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var series = new System.Collections.Generic.List<string>();
	var playing = 0;
	var lastLength = 0f;
	for (var i = 0; i < samples; i++) {
		body.heartRate = heartRate;
		body.bloodOxygen = bloodOxygen;
		body.bloodPressure = bloodPressure;
		body.consciousness = consciousness;
		if (i == 0) {
			var closed = WoundView.view;
			if (closed != null && !closed.gameObject.activeInHierarchy) { PlayerCamera.main.WoundViewButton(); }
		}
		var sources = UnityEngine.Object.FindObjectsOfType<UnityEngine.AudioSource>();
		var best = 0f;
		for (var s = 0; s < sources.Length; s++) {
			var source = sources[s];
			var clip = source.clip;
			if (clip == null) { continue; }
			if (clip.name.IndexOf("thump") < 0) { continue; }
			if (!source.isPlaying) { continue; }
			playing++;
			if (source.time > best) { best = source.time; }
			lastLength = clip.length;
		}
		series.Add(((int)(best * 1000f)).ToString(inv));
		System.Threading.Thread.Sleep(16);
	}
	var view = WoundView.view;
	return "{\"ok\":true,\"held\":true,\"samples\":" + samples.ToString(inv)
		+ ",\"audio\":\"" + string.Join(",", series) + "\""
		+ ",\"playing\":" + playing.ToString(inv)
		+ ",\"length\":" + lastLength.ToString("0.###", inv)
		+ ",\"panelOpen\":" + (view != null && view.gameObject.activeInHierarchy ? "true" : "false") + "}";
}))()
