// recipe: heart-hold-sample
// args: heartRate=n bloodOxygen=n bloodPressure=n consciousness=n
// serves: host-heartbeat-sound-truncated (the beeps under a HELD critical state with a high heart rate)
// returns: ok, heartRate, bloodOxygen, bloodPressure, playing, thumpTime, thumpLength, panelOpen, panelPlaying, panelTime, error, detail
//
// ONE sample: rewrites the local body's critical vitals, ensures the panel is open, then reads the per-beat thump source
// and the panel's alert source. Called once per batch step, the rewrite defeats the decay that made a single forced state
// useless, and the sample is taken with the frames still running (no sleep in here: a stalled main thread stops the very
// Update that plays the sounds).
//
// The series the batch wants is a list of thumpTime values: at a high heart rate the clip (0.769 s) is longer than the
// interval between beats, so a series of positions that never approach the clip's end is the game restarting the clip per
// beat — measured, not assumed.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var heartRate = {{n:heartRate}};
	var bloodOxygen = {{n:bloodOxygen}};
	var bloodPressure = {{n:bloodPressure}};
	var consciousness = {{n:consciousness}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	body.heartRate = heartRate;
	body.bloodOxygen = bloodOxygen;
	body.bloodPressure = bloodPressure;
	body.consciousness = consciousness;
	// A brain that is already dying re-derives consciousness to zero every frame, so the held state must include it: a probe
	// that writes only consciousness reads an unconscious body forever (the lesson of this recipe's second run).
	body.brainHealth = 100f;
	body.energy = 100f;
	var view = WoundView.view;
	if (view != null && !view.gameObject.activeInHierarchy) { PlayerCamera.main.WoundViewButton(); }
	var sources = UnityEngine.Object.FindObjectsOfType<UnityEngine.AudioSource>();
	var playing = 0;
	var thumpTime = 0f;
	var thumpLength = 0f;
	for (var i = 0; i < sources.Length; i++) {
		var source = sources[i];
		var clip = source.clip;
		if (clip == null) { continue; }
		if (clip.name.IndexOf("thump") < 0) { continue; }
		if (!source.isPlaying) { continue; }
		playing++;
		if (source.time > thumpTime) { thumpTime = source.time; }
		thumpLength = clip.length;
	}
	var panelPlaying = false;
	var panelTime = 0f;
	var panelAudio = view == null ? null : view.GetComponent<UnityEngine.AudioSource>();
	if (panelAudio != null) { panelPlaying = panelAudio.isPlaying; panelTime = panelAudio.time; }
	return "{\"ok\":true"
		+ ",\"heartRate\":" + heartRate.ToString("0.##", inv)
		+ ",\"bloodOxygen\":" + bloodOxygen.ToString("0.##", inv)
		+ ",\"bloodPressure\":" + bloodPressure.ToString("0.##", inv)
		+ ",\"playing\":" + playing.ToString(inv)
		+ ",\"thumpTime\":" + thumpTime.ToString("0.###", inv)
		+ ",\"thumpLength\":" + thumpLength.ToString("0.###", inv)
		+ ",\"panelOpen\":" + (view != null && view.gameObject.activeInHierarchy ? "true" : "false")
		+ ",\"panelPlaying\":" + (panelPlaying ? "true" : "false")
		+ ",\"panelTime\":" + panelTime.ToString("0.###", inv) + "}";
}))()
