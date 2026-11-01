// recipe: audio-watch-start
// args: match=s frames=n
// returns: ok, started, error, detail
//
// Starts the declared `audio-watch` recorder (the `audio-watch` eval declaration), which samples every AudioSource each frame and
// keeps the distinct clip names it saw. A one-shot clip is exactly what a polling reader misses - measured 2026-11-02: the
// amputation's gore clip finished between two `audio-recent` calls.
((System.Func<string>)(() => {
	var match = {{s:match}};
	var frames = (int){{n:frames}};
	var started = AcceptanceAudioWatch.Start(match, frames);
	return "{\"ok\":true,\"started\":\"" + started + "\",\"match\":\"" + match + "\",\"frames\":" + frames.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()