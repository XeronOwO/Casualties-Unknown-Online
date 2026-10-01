// recipe: time-scale
// args: speed=s announce=n
// serves: world-acceleration-survives-movement, world-time-local-initiation, world-acceleration-quake-direct-write
// returns: ok, requested, announce, timeScale, curTimeScale, paused
//
// One native speed change, invoked in process. The accelerate keys call PlayerCamera.SetTimeScale(speed,
// switchSound: true, force: false) and the movement rule calls the same method with switchSound: false
// (reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:885-895 and :921-924) — announce=1 reproduces
// the key, announce=0 the silent movement/event reset, so either call travels the CUO patch exactly as
// the game's own site would.
((System.Func<string>)(() => {
	var name = {{s:speed}};
	var announce = {{n:announce}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-local-camera\"}"; }
	PlayerCamera.SpeedType speed;
	if (name == "normal") { speed = PlayerCamera.SpeedType.Normal; }
	else if (name == "fast") { speed = PlayerCamera.SpeedType.Fast; }
	else if (name == "superfast") { speed = PlayerCamera.SpeedType.SuperFast; }
	else { return "{\"ok\":false,\"error\":\"unknown-speed\"}"; }
	camera.SetTimeScale(speed, announce != 0, false);
	return "{\"ok\":true"
		+ ",\"requested\":\"" + name + "\""
		+ ",\"announce\":" + (announce != 0 ? "true" : "false")
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"curTimeScale\":\"" + camera.curTimeScale + "\""
		+ ",\"paused\":" + (PauseHandler.paused ? "true" : "false") + "}";
}))()
