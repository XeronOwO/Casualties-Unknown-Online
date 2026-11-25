// recipe: block-find-breakable
// args: count=n minOffset=n radius=n
// returns: ok, count, bodyX, bodyY, cells (x, y, block), error
//
// Offers breakable cells near the local body, nearest first and all around it. One evaluator call: the block-sync
// case used to read one cell per round-trip across a 65x33 band. Callers still probe a cell with `block-probe`
// before swinging, because a block number also covers walls and background tiles.
//
// A cell's `y` is in the same frame as `bodyY` (the body's own cell), because callers read the offered cell with
// `block-read-at` and compare the two. The loop starts one row below the body (a body stands on the block it wants to
// dig), so the offset is added back before the cell is named: measured 2026-11-26 (`.acceptance/wf-9`), the row took
// an offered cell at (7,477), `block-read-at` answered `block 0` for it, and the cell the grid really held was the one
// directly above in the loop's own frame.
((System.Func<string>)(() => {
	var count = {{n:count}};
	var minOffset = {{n:minOffset}};
	var radius = {{n:radius}};
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var blocksField = world.GetType().GetField("worldBlocks", flags);
	var blocks = blocksField == null ? null : blocksField.GetValue(world) as System.Array;
	if (blocks == null || blocks.Rank != 2) { return "{\"ok\":false,\"error\":\"no-worldblocks\"}"; }
	var startX = (int)System.Math.Floor(camera.body.transform.position.x);
	var startY = (int)System.Math.Floor(camera.body.transform.position.y) - 1;
	// The cells below are named in the body's own frame, so the one-row step down is added back.
	var bodyCellY = startY + 1;
	var lowerX = blocks.GetLowerBound(0);
	var upperX = blocks.GetUpperBound(0);
	var lowerY = blocks.GetLowerBound(1);
	var upperY = blocks.GetUpperBound(1);
	var candidates = new System.Collections.Generic.List<int[]>();
	for (var dy = -radius; dy <= radius; dy++) {
		for (var dx = -radius; dx <= radius; dx++) {
			if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) < minOffset) { continue; }
			candidates.Add(new[] { dx, dy, dx * dx + dy * dy });
		}
	}
	candidates.Sort((a, b) => a[2].CompareTo(b[2]));
	var rows = new System.Collections.Generic.List<string>();
	for (var i = 0; i < candidates.Count && rows.Count < count; i++) {
		var x = startX + candidates[i][0];
		var y = bodyCellY + candidates[i][1];
		if (x < lowerX || x > upperX || y < lowerY || y > upperY) { continue; }
		var block = System.Convert.ToUInt16(blocks.GetValue(x, y));
		if (block == 0 || block == 14) { continue; }
		if (world.GetBlockInfo(block).health > 1000f) { continue; }
		rows.Add("{\"x\":" + x + ",\"y\":" + y + ",\"block\":" + block + "}");
	}
	return "{\"ok\":true,\"count\":" + rows.Count + ",\"bodyX\":" + startX + ",\"bodyY\":" + (startY + 1) + ",\"cells\":[" + string.Join(",", rows) + "]}";
}))()
