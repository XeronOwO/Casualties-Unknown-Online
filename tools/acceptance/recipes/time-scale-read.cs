// recipe: time-scale-read
// args: none
// serves: world-acceleration-survives-movement, world-time-local-initiation, world-acceleration-quake-direct-write
// returns: ok, timeScale, curTimeScale, paused, sleeping, conscious, alive, earthquakeTime, earthquakeIntensity, earthquakeDelay
//
// The live clock as one client sees it, plus the state around it: Time.timeScale is what the simulation
// runs at, PlayerCamera.curTimeScale is the game's own speed field — what the HUD's icon row reads
// (PlayerCamera.cs:2146-2159) — and the WorldGeneration fields say whether a quake is running.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-local-camera\"}"; }
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	var body = camera.body;
	var world = WorldGeneration.world;
	return "{\"ok\":true"
		+ ",\"timeScale\":" + num(UnityEngine.Time.timeScale)
		+ ",\"curTimeScale\":\"" + camera.curTimeScale + "\""
		+ ",\"paused\":" + (PauseHandler.paused ? "true" : "false")
		+ ",\"sleeping\":" + (body != null && body.sleeping ? "true" : "false")
		+ ",\"conscious\":" + (body != null && body.conscious ? "true" : "false")
		+ ",\"alive\":" + (body != null && body.alive ? "true" : "false")
		+ ",\"earthquakeTime\":" + (world != null ? num(world.earthquakeTime) : "-1")
		+ ",\"earthquakeIntensity\":" + (world != null ? num(world.earthquakeIntensity) : "-1")
		+ ",\"earthquakeDelay\":" + (world != null ? num(world.earthquakeDelay) : "-1") + "}";
}))()
