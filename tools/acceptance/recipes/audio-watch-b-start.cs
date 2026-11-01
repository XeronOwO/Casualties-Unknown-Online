// recipe: audio-watch-b-start
// args: match=s frames=n
// returns: ok, started, error, detail
((System.Func<string>)(() => {
	var started = AcceptanceAudioWatchB.Start({{s:match}}, (int){{n:frames}});
	return "{\"ok\":true,\"started\":\"" + started + "\"}";
}))()