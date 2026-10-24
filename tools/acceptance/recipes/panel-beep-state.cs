// recipe: panel-beep-state
// args: none
// serves: host-heartbeat-sound-truncated (the medical panel's critical beep, which the user hears cut)
// returns: ok, open, playing, time, length, critical, pressure, consciousness, error, detail
//
// Reads the open wound view's OWN alert source next to the flag that drives it. `WoundView.Update` plays its looping alert
// while `!WorldGeneration.unchipped && body.isCriticallyDying` and STOPS the source the moment that flag drops - so a flag
// that flickers turns one continuous beep into a restart per flicker, which is the truncation. This probe answers whether
// the source is playing across consecutive reads, its position inside the clip, and the flag's own inputs.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var view = WoundView.view;
	if (view == null) { return "{\"ok\":true,\"open\":false}"; }
	var body = view.body;
	var critical = false;
	var pressure = 0f;
	var consciousness = 0f;
	if (body != null) {
		critical = body.isCriticallyDying;
		pressure = body.bloodPressure;
		consciousness = body.consciousness;
	}
	var audio = view.GetComponent<UnityEngine.AudioSource>();
	var playing = false;
	var time = 0f;
	var length = 0f;
	if (audio != null) {
		playing = audio.isPlaying;
		time = audio.time;
		length = audio.clip == null ? 0f : audio.clip.length;
	}
	return "{\"ok\":true,\"open\":true"
		+ ",\"playing\":" + (playing ? "true" : "false")
		+ ",\"time\":" + time.ToString("0.###", inv)
		+ ",\"length\":" + length.ToString("0.###", inv)
		+ ",\"critical\":" + (critical ? "true" : "false")
		+ ",\"pressure\":" + pressure.ToString("0.##", inv)
		+ ",\"consciousness\":" + consciousness.ToString("0.##", inv) + "}";
}))()
