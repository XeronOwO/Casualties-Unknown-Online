// recipe: heart-beat-probe
// args: heartRate=n bloodOxygen=n bloodPressure=n consciousness=n
// serves: host-heartbeat-sound-truncated (the per-beat thump in the panel + critical case)
// returns: ok, heartRate, heartProg, didThump, thumps (clip:time/length pairs), panelClip, panelTime, panelOpen, error, detail
//
// One sample of the state the user described: the medical panel open, the body critical, a real heart rate present. It holds
// the vitals (including brain health, which otherwise re-derives consciousness to zero) and then reads
// - the body's own beat machine (`heartProg` / `didThump`, which `Body.cs` uses to decide when to play a thump),
// - every AudioSource whose clip name contains "thump", with its clip, position and length,
// - the panel's own alert source and its clip name.
// A series of these one per batch step shows which clip is restarted and how often: the beat clip is 0.769 s, so at a high
// heart rate every thump cuts the previous one, and the reading names the clip that does it.
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
	body.brainHealth = 100f;
	body.energy = 100f;
	var view = WoundView.view;
	if (view != null && !view.gameObject.activeInHierarchy) { PlayerCamera.main.WoundViewButton(); }
	var parts = new System.Collections.Generic.List<string>();
	var sources = UnityEngine.Object.FindObjectsOfType<UnityEngine.AudioSource>();
	var panelClip = "";
	var panelTime = 0f;
	var panelAudio = view == null ? null : view.GetComponent<UnityEngine.AudioSource>();
	if (panelAudio != null) {
		panelClip = panelAudio.clip == null ? "" : panelAudio.clip.name;
		panelTime = panelAudio.time;
	}
	for (var i = 0; i < sources.Length; i++) {
		var source = sources[i];
		var clip = source.clip;
		if (clip == null) { continue; }
		if (clip.name.IndexOf("thump") < 0) { continue; }
		parts.Add(clip.name + ":" + source.time.ToString("0.###", inv) + "/" + clip.length.ToString("0.###", inv) + ":" + (source.isPlaying ? "1" : "0"));
	}
	return "{\"ok\":true"
		+ ",\"heartRate\":" + heartRate.ToString("0.##", inv)
		+ ",\"heartProg\":" + body.heartProg.ToString("0.###", inv)
		+ ",\"didThump\":" + (body.didThump ? "true" : "false")
		+ ",\"thumps\":\"" + string.Join(",", parts) + "\""
		+ ",\"panelClip\":\"" + panelClip + "\""
		+ ",\"panelTime\":" + panelTime.ToString("0.###", inv)
		+ ",\"panelOpen\":" + (view != null && view.gameObject.activeInHierarchy ? "true" : "false") + "}";
}))()
