// recipe: crit-source-state
// args: none
// serves: host-heartbeat-sound-truncated (its first reading: which clip the critical loop plays and whether it restarts)
// returns: ok, hasManager, hasSource, clip, isPlaying, time, volume, loop, error, detail
//
// Reads the game's own critical-loop source (`MusicManager.main.critSource`) on THIS client: the clip it carries, whether
// it is playing, the position inside the clip and its volume. The ticket's question is whether that position RESETS
// between two readings while the body is in a critical state - a reset is the truncated heartbeat, and the reading that
// shows it also names the source every candidate fix must leave alone.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var manager = MusicManager.main;
	if (manager == null) { return "{\"ok\":false,\"hasManager\":false}"; }
	var source = manager.critSource;
	if (source == null) { return "{\"ok\":true,\"hasManager\":true,\"hasSource\":false}"; }
	var clipName = source.clip == null ? "" : source.clip.name;
	return "{\"ok\":true,\"hasManager\":true,\"hasSource\":true"
		+ ",\"clip\":\"" + clipName.Replace("\"", "'") + "\""
		+ ",\"isPlaying\":" + (source.isPlaying ? "true" : "false")
		+ ",\"time\":" + source.time.ToString("0.###", inv)
		+ ",\"volume\":" + source.volume.ToString("0.###", inv)
		+ ",\"loop\":" + (source.loop ? "true" : "false") + "}";
}))()
