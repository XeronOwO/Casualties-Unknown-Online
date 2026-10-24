// recipe: heart-audio-state
// args: none
// serves: host-heartbeat-sound-truncated (the local body's own critical heartbeat, read where it plays)
// returns: ok, count, entries (clip, length, time, playing, volume, loop), error, detail
//
// Samples every AudioSource in this scene whose clip name looks like a heartbeat or the critical loop, and reports the
// position inside the clip next to the clip's LENGTH. Two readings a fraction of a second apart answer the ticket's
// question directly: a clip whose `time` jumps BACKWARDS, or whose `playing` goes false while `time < length`, is the
// truncation the user hears - and the entry names the clip.
//
// It reads the local scene only (no session involvement): the sound the host hears for its own body is on this client.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var all = UnityEngine.Object.FindObjectsOfType<AudioSource>();
	var parts = new System.Collections.Generic.List<string>();
	var count = 0;
	for (var i = 0; i < all.Length; i++) {
		var source = all[i];
		var clip = source.clip;
		if (clip == null) { continue; }
		var name = clip.name.ToLowerInvariant();
		if (name.IndexOf("heart") < 0 && name.IndexOf("thump") < 0 && name.IndexOf("critic") < 0) { continue; }
		count++;
		parts.Add("{\"clip\":\"" + clip.name.Replace("\"", "'") + "\""
			+ ",\"length\":" + clip.length.ToString("0.###", inv)
			+ ",\"time\":" + source.time.ToString("0.###", inv)
			+ ",\"playing\":" + (source.isPlaying ? "true" : "false")
			+ ",\"volume\":" + source.volume.ToString("0.###", inv)
			+ ",\"loop\":" + (source.loop ? "true" : "false") + "}");
	}
	return "{\"ok\":true,\"count\":" + count.ToString(inv) + ",\"entries\":[" + string.Join(",", parts) + "]}";
}))()
