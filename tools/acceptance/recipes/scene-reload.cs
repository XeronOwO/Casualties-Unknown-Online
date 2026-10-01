// recipe: scene-reload
// args: none
// serves: world-acceleration-quake-direct-write
// returns: ok, started, scene, timeScale, curTimeScale, error, detail
//
// Produces the game's own scene reload: WorldGeneration.ReloadScene()
// (reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:1033-1039) runs Clear(), then
// `Time.timeScale = 1f;`, then SceneManager.LoadScene(active scene name). That write site is one of the
// game's four DIRECT clock writers, and it has no caller in the decompiled assembly (whole-tree scan),
// so no player path reaches it: this recipe starts the game's own coroutine through the evaluator, one
// eval per invocation, and the record declares the forced trigger. The reflection lookup runs BEFORE the
// world check, so a lobby invocation exercises the lookup and the answer contract without reloading
// anything; the live invocation runs with a standing acceleration on the host, where the CUO pump's
// `host direct timeScale write 1 adopted as Normal.` line is exactly the adoption the row expects NOT
// to see.
((System.Func<string>)(() => {
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
	var method = typeof(WorldGeneration).GetMethod("ReloadScene", flags);
	if (method == null) { return "{\"ok\":false,\"error\":\"reload-scene-not-found\"}"; }
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-live-world\"}"; }
	var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
	var camera = PlayerCamera.main;
	var routine = method.Invoke(world, null) as System.Collections.IEnumerator;
	if (routine == null) { return "{\"ok\":false,\"error\":\"reload-scene-not-an-enumerator\"}"; }
	world.StartCoroutine(routine);
	return "{\"ok\":true,\"started\":true"
		+ ",\"scene\":\"" + scene + "\""
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"curTimeScale\":\"" + (camera == null ? "none" : System.Convert.ToString(camera.curTimeScale)) + "\"}";
}))()
