// recipe: layer-time-set
// args: seconds=n
// serves: save-layer-time-not-carried
// returns: ok, before, after, error, detail
//
// Writes the live layer timer the game accumulates (WorldGeneration.world.layerTimeSpent), so the
// "six of ten minutes already spent" precondition of a continue does not cost a real six-minute
// wait. A forced input, declared per row.
((System.Func<string>)(() => {
	var seconds = {{n:seconds}};
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-live-world\"}"; }
	var before = world.layerTimeSpent;
	world.layerTimeSpent = seconds;
	var after = world.layerTimeSpent;
	return "{\"ok\":true,\"before\":" + before.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"after\":" + after.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
