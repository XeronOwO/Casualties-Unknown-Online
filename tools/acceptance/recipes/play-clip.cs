// recipe: play-clip
// args: clip=s twoDimensional=s
// returns: ok, clip, present, played, error, detail
//
// Plays one clip through the game's own `Sound.Play(string, …)` at the local body's position, with the 2D flag chosen by the caller. It exists to separate
// two things a listener-side reading cannot: whether the peer's REPLAY path reaches `Sound.Play` at all, and whether the clip is audible when it does.
// `twoDimensional=true` is the flag the lockpick-failure report carries (`[Pain, gore2, …, False, True, 0]` in `report-watch`).
((System.Func<string>)(() => {
	var clip = {{s:clip}};
	var twoDimensional = {{s:twoDimensional}} == "true";
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var loaded = Resources.Load<AudioClip>("Sounds/" + clip);
	var pos = new Vector2(camera.body.transform.position.x, camera.body.transform.position.y);
	Sound.Play(clip, pos, twoDimensional, true, null, 1f, 1f, false, false);
	return "{\"ok\":true,\"clip\":\"" + clip + "\",\"present\":" + (loaded != null ? "true" : "false") + ",\"played\":true}";
}))()
