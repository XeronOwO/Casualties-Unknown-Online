// recipe: last-layer-read
// args: none
// returns: ok, layer, unchipped, blockItemUsage, endLab, doingEscape, attackCooldown, attackRot, error
//
// Reads each client's own last-layer gate: the native layer index, the run's
// unchipped switch, the local PlayerCamera gate, the local EndLabScript state,
// and the local body's native attack fields. It never reads the peer.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	var end = EndLabScript.instance;
	var cooldown = body == null ? -1f : body.attackCooldown;
	var rot = body == null ? 0f : body.attackRot;
	return "{\"ok\":true,\"layer\":" + world.biomeDepth
		+ ",\"unchipped\":" + (WorldGeneration.unchipped ? "true" : "false")
		+ ",\"blockItemUsage\":" + (camera.blockItemUsage ? "true" : "false")
		+ ",\"endLab\":" + (end != null ? "true" : "false")
		+ ",\"doingEscape\":" + (end != null && end.doingEscape ? "true" : "false")
		+ ",\"bodyX\":" + (body == null ? 0f : body.transform.position.x).ToString("0.###", inv)
		+ ",\"bodyY\":" + (body == null ? 0f : body.transform.position.y).ToString("0.###", inv)
		+ ",\"generating\":" + (world.generatingWorld ? "true" : "false")
		+ ",\"paused\":" + (PauseHandler.main != null && PauseHandler.main.isPaused ? "true" : "false")
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", inv)
		+ ",\"attackCooldown\":" + cooldown.ToString("0.###", inv)
		+ ",\"attackRot\":" + rot.ToString("0.###", inv) + "}";
}))()
