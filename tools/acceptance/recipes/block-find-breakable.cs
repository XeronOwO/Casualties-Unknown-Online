// recipe: block-find-breakable
// args: count=n minOffset=n radius=n
// returns: ok, count, cells (x, y, block), error
//
// Finds breakable terrain cells near the local body in ONE evaluator call. The
// block-sync case used to read one cell per driver round-trip across a 65x33
// band (2,145 evals); the live table is local, so the scan belongs here.
((System.Func<string>)(() => {
	var count = {{n:count}};
	// A cell directly under the player projects just past the bottom of the drawn area on this camera; a scan that starts
	// at `minOffset` 1 offers the ones beside them instead, which is where a player actually swings (measured 2026-11-22:
	// twenty-four cells at the player's own column all landed on screen row 732 of 720).
	// Cells are offered nearest-first and all around the player, so a caller can pick one that lands in the part of the
	// drawn area a player can actually aim at. The first version walked the column above the player first, and on this
	// camera those cells sit under the bottom bar where `PlayerCamera.HandleAttacks` refuses every swing
	// (`PlayerCamera.cs:1997`, measured 2026-11-22: twenty-four candidates all at screen row 682–732 of 720).
	var minOffset = {{n:minOffset}};
	var radius = {{n:radius}};
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
	var candidates = new System.Collections.Generic.List<int[]>();
	for (var dy = -radius; dy <= radius; dy++) {
		for (var dx = -radius; dx <= radius; dx++) {
			if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) < minOffset) { continue; }
			candidates.Add(new[] { dx, dy, dx * dx + dy * dy });
		}
	}
	candidates.Sort((a, b) => a[2].CompareTo(b[2]));
	for (var i = 0; i < candidates.Count && rows.Count < count; i++) {
		var x = startX + candidates[i][0];
		var y = startY + candidates[i][1];
		if (x < lowerX || x > upperX || y < lowerY || y > upperY) { continue; }
		var block = System.Convert.ToUInt16(blocks.GetValue(x, y));
		// 0 is air, 14 is the life pod shell whose BlockInfo row does not exist.
		if (block == 0 || block == 14) { continue; }
		// The callers damage the cell with 1,000; a tougher block keeps its sprite and
		// cannot witness the damage path (measured 2026-11-16: block 6 survived 1,000).
		if (world.GetBlockInfo(block).health > 1000f) { continue; }
		// A block number in the world grid is not yet something a swing can hit: the game damages only colliders
		// tagged `BlockGround` (Body.cs:2162-2168), and the grid also carries background and wall blocks whose
		// collider either does not exist at that point or carries another tag (measured 2026-11-22: the first
		// candidate held only a dropped item's collider, tag "Untagged", layer "Item").
		var at = new Vector2(x + 0.5f, y + 0.5f);
		var under = Physics2D.OverlapPointAll(at);
		var ground = false;
		for (var h = 0; h < under.Length; h++) { if (under[h].gameObject.CompareTag("BlockGround")) { ground = true; break; } }
		if (!ground) { continue; }
		rows.Add("{\"x\":" + x + ",\"y\":" + y + ",\"block\":" + block + "}");
	}
	return "{\"ok\":true,\"count\":" + rows.Count + ",\"bodyX\":" + startX + ",\"bodyY\":" + (startY + 1) + ",\"cells\":[" + string.Join(",", rows) + "]}";
}))()
