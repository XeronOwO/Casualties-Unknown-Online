// recipe: radiation-line-state
// args: none
// serves: radiation-line-descent-rate-per-side
// returns: ok, active, timeGone, lineY, halfHeight, bodyY, conscious, alive, error, detail
//
// Reads the live radiation line: its `active` flag, the private `timeGone` descent the native Update advances
// (`RadiationLine.cs`), the world position derived from it, and the local body's own state (the native advance's
// multiplier is `conscious ? 1 : 0.2`). A run reads this on BOTH clients a second apart while one side is forced
// unconscious, which is how the descent RATE is compared instead of eyeballed.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var line = RadiationLine.line;
	if (line == null) { return "{\"ok\":false,\"error\":\"no-line\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var timeGone = HarmonyLib.Traverse.Create(line).Field("timeGone").GetValue<float>();
	var world = WorldGeneration.world;
	var lineY = line.transform.position.y;
	var halfHeight = world != null ? world.height : 0f;
	return "{\"ok\":true"
		+ ",\"active\":" + (line.active ? "true" : "false")
		+ ",\"timeGone\":" + timeGone.ToString("0.###", inv)
		+ ",\"lineY\":" + lineY.ToString("0.###", inv)
		+ ",\"halfHeight\":" + halfHeight.ToString("0.###", inv)
		+ ",\"bodyY\":" + body.transform.position.y.ToString("0.###", inv)
		+ ",\"conscious\":" + (body.conscious ? "true" : "false")
		+ ",\"alive\":" + (body.alive ? "true" : "false") + "}";
}))()
