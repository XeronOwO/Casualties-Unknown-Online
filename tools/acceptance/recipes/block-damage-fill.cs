// recipe: block-damage-fill
// args: dx=n dy=n step=n count=n dmg=n
// serves: block-damage-table-capacity-alignment, guest-partial-block-damage-re-report
// returns: ok, damaged, scanned, complete, broke, outOfRange, dmg, tableBefore, tableAfter, evicted,
//          firstCell, lastCell, sample, error, detail
//
// Fills the game's own damaged-cell list (WorldGeneration.world.blockDamages, with the game's own cap
// and FIFO eviction at WorldGeneration.cs:731-737) by rolling the game's own DamageBlock at up to
// <count> distinct cells in one horizontal band that starts <dx>/<dy> cells from the local body and
// steps <step> cells per try. Air cells (block 0), cells whose type health is <= <dmg> and cells
// outside the world are skipped; the band scans at most count * 8 cells, so `complete` says whether the
// fill reached `count` instead of the driver reading an under-delivered setup as a success. `broke`
// counts cells whose block changed under the roll - a fresh partial hit cannot break one, but a band
// over cells that already carry accumulated damage can (DamageBlock adds to an existing row and breaks
// at damage >= health), and this answer makes that visible. `evicted` is how many rows the table lost
// while filling (tableBefore + damaged - tableAfter). The cap itself is a literal inside the game's own
// method (WorldGeneration.cs:732) and is deliberately NOT restated as a constant here. One eval.
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (world == null || body == null) { return "{\"ok\":false,\"error\":\"no-world-or-body\"}"; }
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var dx = (int)({{n:dx}});
	var dy = (int)({{n:dy}});
	var step = (int)({{n:step}});
	var count = (int)({{n:count}});
	var dmg = (float)({{n:dmg}});
	if (step < 1 || count < 1 || dmg <= 0f) { return "{\"ok\":false,\"error\":\"bad-args\",\"detail\":\"step>=1, count>=1 and dmg>0 are required\"}"; }
	var start = world.WorldToBlockPos(body.transform.position);
	var tableBefore = world.blockDamages.Count;
	var damaged = 0;
	var scanned = 0;
	var broke = 0;
	var outOfRange = 0;
	var firstX = 0;
	var firstY = 0;
	var lastX = 0;
	var lastY = 0;
	var sb = new System.Text.StringBuilder();
	for (var i = 0; i < count * 8 && damaged < count; i++) {
		var cell = new Vector2Int(start.x + dx + i * step, start.y + dy);
		scanned++;
		if (cell.x < 0 || cell.y < 0 || cell.x >= (int)world.width || cell.y >= (int)world.height) { outOfRange++; continue; }
		var block = world.GetBlock(cell);
		if (block == 0) { continue; }
		var info = world.GetBlockInfo(block);
		if (info == null || info.health <= dmg) { continue; }
		world.DamageBlock(cell, dmg, false, false, true);
		if (world.GetBlock(cell) != block) { broke++; }
		damaged++;
		if (damaged == 1) { firstX = cell.x; firstY = cell.y; }
		lastX = cell.x;
		lastY = cell.y;
		if (damaged <= 8) {
			if (sb.Length > 0) { sb.Append(','); }
			sb.Append("{\"x\":").Append(cell.x.ToString(inv)).Append(",\"y\":").Append(cell.y.ToString(inv)).Append('}');
		}
	}
	return "{\"ok\":true,\"damaged\":" + damaged.ToString(inv)
		+ ",\"scanned\":" + scanned.ToString(inv)
		+ ",\"complete\":" + (damaged >= count ? "true" : "false")
		+ ",\"broke\":" + broke.ToString(inv)
		+ ",\"outOfRange\":" + outOfRange.ToString(inv)
		+ ",\"dmg\":" + dmg.ToString("0.###", inv)
		+ ",\"tableBefore\":" + tableBefore.ToString(inv)
		+ ",\"tableAfter\":" + world.blockDamages.Count.ToString(inv)
		+ ",\"evicted\":" + System.Math.Max(0, tableBefore + damaged - world.blockDamages.Count).ToString(inv)
		+ ",\"firstCell\":{\"x\":" + firstX.ToString(inv) + ",\"y\":" + firstY.ToString(inv) + "}"
		+ ",\"lastCell\":{\"x\":" + lastX.ToString(inv) + ",\"y\":" + lastY.ToString(inv) + "}"
		+ ",\"sample\":[" + sb.ToString() + "]}";
}))()
