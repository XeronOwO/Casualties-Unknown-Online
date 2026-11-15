// recipe: block-find-breakable
// args: count=n
// returns: ok, count, cells (x, y, block), error
//
// Finds breakable terrain cells near the local body in ONE evaluator call. The
// block-sync case used to read one cell per driver round-trip across a 65x33
// band (2,145 evals); the live table is local, so the scan belongs here.
((System.Func<string>)(() => {
	var count = {{n:count}};
	var world = WorldGeneration.world;
	var camera = PlayerCamera.main;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var worldType = world.GetType();
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var blocksField = worldType.GetField("worldBlocks", flags);
	var blocks = blocksField == null ? null : blocksField.GetValue(world) as System.Array;
	if (blocks == null || blocks.Rank != 2) { return "{\"ok\":false,\"error\":\"no-worldblocks\"}"; }
	var startX = (int)System.Math.Floor(camera.body.transform.position.x);
	var startY = (int)System.Math.Floor(camera.body.transform.position.y) - 1;
	var rows = new System.Collections.Generic.List<string>();
	var lowerX = blocks.GetLowerBound(0);
	var upperX = blocks.GetUpperBound(0);
	var lowerY = blocks.GetLowerBound(1);
	var upperY = blocks.GetUpperBound(1);
	for (var y = startY + 24; y >= startY - 40 && rows.Count < count; y--) {
		for (var offset = 0; offset <= 16 && rows.Count < count; offset++) {
			var xs = offset == 0 ? new[] { startX } : new[] { startX - offset, startX + offset };
			for (var xi = 0; xi < xs.Length && rows.Count < count; xi++) {
				var x = xs[xi];
				if (x < lowerX || x > upperX || y < lowerY || y > upperY) { continue; }
				var block = System.Convert.ToUInt16(blocks.GetValue(x, y));
				// 0 is air, 14 is the life pod shell whose BlockInfo row does not exist.
				if (block == 0 || block == 14) { continue; }
				rows.Add("{\"x\":" + x + ",\"y\":" + y + ",\"block\":" + block + "}");
			}
		}
	}
	return "{\"ok\":true,\"count\":" + rows.Count + ",\"cells\":[" + string.Join(",", rows) + "]}";
}))()
