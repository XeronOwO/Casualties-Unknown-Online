// recipe: layer-limit-set
// args: seconds=n
// serves: radiation-line-descent-rate-per-side (the line activates only past maxTimePerLayer, and
//         that field is written once at world creation from the `timelimit` run setting)
// returns: ok, before, after, error, detail
//
// Writes WorldGeneration.maxTimePerLayer on THIS client's world, so the game's own Update can cross
// the threshold with a layer time the fixture sets. Setup drive, not a read: the fixture restores a
// sane limit afterwards by setting it again.
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var seconds = {{n:seconds}};
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var field = typeof(WorldGeneration).GetField("maxTimePerLayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	if (field == null) { return "{\"ok\":false,\"error\":\"no-field\"}"; }
	var before = (float)field.GetValue(world);
	field.SetValue(world, (float)seconds);
	var after = (float)field.GetValue(world);
	return "{\"ok\":true,\"before\":" + before.ToString("0.###", inv) + ",\"after\":" + after.ToString("0.###", inv) + "}";
}))()
