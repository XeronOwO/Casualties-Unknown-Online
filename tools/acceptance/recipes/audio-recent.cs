// recipe: audio-recent
// args: match=s
// returns: ok, count, playing, error, detail
//
// Samples every AudioSource in this scene and answers the clip names that are playing right now, optionally filtered by a substring
// (`match`, case-insensitive). It serves the sound tickets' "every peer hears X once" rows from the LISTENER side: stage the action on
// one client and sample this on another while the clip is in its window — the clip name is the game's own (`Sound.Play` takes it), so a
// match is the peer playing the sound, not a log line about it.
//
// Read-only. A batch that needs the whole window samples in a loop; a single call answers only this instant.
((System.Func<string>)(() => {
	var match = {{s:match}};
	var sources = UnityEngine.Object.FindObjectsOfType<UnityEngine.AudioSource>();
	var names = "";
	var playing = 0;
	for (var i = 0; i < sources.Length; i++) {
		var source = sources[i];
		if (source == null || !source.isPlaying || source.clip == null) { continue; }
		var clip = source.clip.name;
		if (match.Length > 0 && clip.IndexOf(match, System.StringComparison.OrdinalIgnoreCase) < 0) { continue; }
		playing++;
		if (names.Length > 0) { names += "|"; }
		names += clip;
	}

	return "{\"ok\":true,\"count\":" + sources.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"playing\":" + playing.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"matches\":\"" + names + "\"}";
}))()
