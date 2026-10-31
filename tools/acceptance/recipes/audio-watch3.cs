// recipe: audio-watch3
// args: match=s frames=n
// returns: ok, started, match, frames, error, detail
//
// Starts the `audio-watch` declaration's coroutine: it samples the scene's AudioSources once per FRAME and records every clip name it
// sees playing that contains `match`, so a short clip cannot fall between two batch polls. Read the result with `audio-watch-read`.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var match = {{s:match}};
	var frames = (int)({{n:frames}});
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type watch = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { watch = assemblies[i].GetType("AcceptanceAudioWatch3", false); } catch { watch = null; }
		if (watch != null) { break; }
	}
	if (watch == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare audio-watch first\"}"; }
	var method = watch.GetMethod("Start");
	if (method == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var started = (string)method.Invoke(null, new object[] { match, frames });
	return "{\"ok\":true,\"started\":\"" + started + "\",\"match\":\"" + match + "\",\"frames\":" + frames.ToString(inv) + "}";
}))()
