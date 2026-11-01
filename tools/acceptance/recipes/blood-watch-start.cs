// recipe: blood-watch-start
// args: frames=n
// returns: ok, started, error, detail
//
// Starts the `blood-watch` declaration: it samples `WoundView.view.body.bloodVolume` once per frame (a coroutine - an eval cannot
// wait for frames, it blocks the main thread), so the caller can read the series after the fact with `blood-watch-read`.
((System.Func<string>)(() => {
	var started = AcceptanceBloodWatch.Start((int){{n:frames}});
	return "{\"ok\":true,\"started\":\"" + started + "\"}";
}))()