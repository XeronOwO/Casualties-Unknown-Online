// recipe: layer-descend
// args: twice=n
// serves: host-blast-rolls-a-second-drop-set (its count row needs a world-generation carrier both clients share)
// returns: ok, twice, worldY, error, detail
//
// Advances one layer through the GAME'S OWN sink: `WorldGeneration.RegenerateWorld(bool twice)` is where every descent in
// the game lands (the end-of-layer panel, the drill pod, and the console's `skiplayer`), and this starts it the way those
// callers do — on the world object, as a coroutine. `twice=1` takes the pod's two-layer step; anything else descends one.
//
// This is staging, not a player entry: the player's own descent is the panel's button.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var twice = (int)({{n:twice}}) != 0;
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	world.StartCoroutine(world.RegenerateWorld(twice));
	return "{\"ok\":true,\"twice\":" + (twice ? "true" : "false")
		+ ",\"worldY\":" + world.transform.position.y.ToString("0.#", inv) + "}";
}))()
