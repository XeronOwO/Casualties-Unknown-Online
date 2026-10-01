// recipe: quake-force
// args: none
// serves: world-acceleration-quake-direct-write
// returns: ok, beforeDelay, earthquakeDelay, earthquakeTime, earthquakeIntensity, timeScale
//
// Starts a REAL native earthquake on this side by putting the game's own countdown where its own roll
// puts it: WorldGeneration.Update starts a quake when earthquakeDelay < 0 and the local body is not
// sleeping, sets earthquakeTime to Random.Range(3f, 25f) and writes Time.timeScale = 1f
// (reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:865-871). The quake itself — the
// intensity ramp, the shakes, the block breaks — is then the game's own code; nothing here writes the
// clock. Force it on the HOST: a guest's timer is frozen by WorldGenerationUpdatePatch and only the
// host's broadcast starts the guest's quake.
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	var before = num(world.earthquakeDelay);
	world.earthquakeDelay = -1f;
	return "{\"ok\":true"
		+ ",\"beforeDelay\":" + before
		+ ",\"earthquakeDelay\":" + num(world.earthquakeDelay)
		+ ",\"earthquakeTime\":" + num(world.earthquakeTime)
		+ ",\"earthquakeIntensity\":" + num(world.earthquakeIntensity)
		+ ",\"timeScale\":" + num(UnityEngine.Time.timeScale) + "}";
}))()
