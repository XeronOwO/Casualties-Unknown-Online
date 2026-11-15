// recipe: set-layer
// args: depth=n
// returns: ok, old, depth, error
//
// Writes the world's current layer index. It exists so a batch can stand one
// layer before the last layer and let the game's own RegenerateWorld take the
// final step; this changes state only and starts no descent.
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var depth = (int)({{n:depth}});
	var old = world.biomeDepth;
	world.biomeDepth = depth;
	return "{\"ok\":true,\"old\":" + old
		+ ",\"depth\":" + world.biomeDepth + "}";
}))()
