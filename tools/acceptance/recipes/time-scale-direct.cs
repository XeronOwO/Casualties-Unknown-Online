// recipe: time-scale-direct
// args: scale=n
// serves: world-acceleration-quake-direct-write
// returns: ok, requested, before, timeScale, curTimeScale
//
// Writes Time.timeScale DIRECTLY, the way the game's own non-SetTimeScale writers do — the quake start
// (reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:870) and the console's timescale command
// (ConsoleScript.cs:815) — with no PlayerCamera call, so no CUO routing sees it. The host's pump adopts a
// domain value as the shared speed and ignores a value that is not one, which is exactly what this recipe
// lets a run produce on demand (including a non-domain value as the negative sample).
((System.Func<string>)(() => {
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	float scale = {{n:scale}};
	var before = num(UnityEngine.Time.timeScale);
	UnityEngine.Time.timeScale = scale;
	return "{\"ok\":true"
		+ ",\"requested\":" + num(scale)
		+ ",\"before\":" + before
		+ ",\"timeScale\":" + num(UnityEngine.Time.timeScale)
		+ ",\"curTimeScale\":\"" + (PlayerCamera.main != null ? PlayerCamera.main.curTimeScale.ToString() : "none") + "\"}";
}))()
