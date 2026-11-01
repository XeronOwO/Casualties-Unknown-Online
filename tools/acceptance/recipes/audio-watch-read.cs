// recipe: audio-watch-read
// args: none
// returns: ok, running, frames, seen, hits, error, detail
//
// Reads what the `audio-watch` recorder has collected: the distinct clip names seen playing since it started, and how many frames it
// has sampled. Separate from the start so a batch can read it after the event it is watching for.
((System.Func<string>)(() => {
	return "{\"ok\":true,\"running\":" + (AcceptanceAudioWatch.Running ? "true" : "false")
		+ ",\"frames\":" + AcceptanceAudioWatch.Frames.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"seen\":" + AcceptanceAudioWatch.Seen.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"hits\":\"" + AcceptanceAudioWatch.Hits + "\"}";
}))()