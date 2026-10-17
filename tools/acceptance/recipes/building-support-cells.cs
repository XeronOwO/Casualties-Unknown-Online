// recipe: building-support-cells
// args: min=n limit=n
// serves: validation-and-source-checks S2b - the machine read its capture loop still owes: the host reads
//         the drop table of EVERY requireGround building standing on the support cell, marked or not, so a
//         cell that carries more than one is the only place that loop can be READ (the census answers
//         per-building rows, which a large map truncates before a shared cell turns up).
// returns: ok, total, requireGround, multiCells, listed, entries (each cellX, cellY, count and its
//          building ids with a hasTable flag), error
//
// Read-only. Groups the live requireGround population by the support cell each entity stands on and lists
// the cells carrying at least <min> of them, up to <limit> cells - the shape `MarkSupportLossRemote`
// iterates, so the run can break one cell's support and read the host's own `[BuildingSupport] captured N
// drop row(s) from M building(s)` line against a known M. Read it after the world has settled.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var min = (int)({{n:min}});
	var limit = (int)({{n:limit}});
	if (min < 2) { min = 2; }
	if (limit < 1) { limit = 1; }
	if (limit > 50) { limit = 50; }
	var all = UnityEngine.Object.FindObjectsOfType<BuildingEntity>();
	if (all == null) { return "{\"ok\":false,\"error\":\"no-census\"}"; }
	var cells = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<BuildingEntity>>();
	var requireGround = 0;
	foreach (var building in all) {
		if (building == null || !building.requireGround) { continue; }
		requireGround++;
		var key = building.blockPlacedOn.x.ToString(inv) + "," + building.blockPlacedOn.y.ToString(inv);
		System.Collections.Generic.List<BuildingEntity> bucket;
		if (!cells.TryGetValue(key, out bucket)) { bucket = new System.Collections.Generic.List<BuildingEntity>(); cells.Add(key, bucket); }
		bucket.Add(building);
	}
	var listed = new System.Collections.Generic.List<string>();
	var multiCells = 0;
	foreach (var pair in cells) {
		if (pair.Value.Count < min) { continue; }
		multiCells++;
		if (listed.Count >= limit) { continue; }
		var first = pair.Value[0];
		var sb = new System.Text.StringBuilder();
		sb.Append("{\"cellX\":").Append(first.blockPlacedOn.x.ToString(inv));
		sb.Append(",\"cellY\":").Append(first.blockPlacedOn.y.ToString(inv));
		sb.Append(",\"count\":").Append(pair.Value.Count.ToString(inv));
		sb.Append(",\"ids\":[");
		for (var i = 0; i < pair.Value.Count; i++) {
			if (i > 0) { sb.Append(','); }
			var building = pair.Value[i];
			var hasTable = (building.alwaysDrop != null && building.alwaysDrop.Length > 0)
				|| (building.itemsDropOnDestroy != null && building.itemsDropOnDestroy.Length > 0)
				|| building.guaranteedDropAmount > 0;
			sb.Append("{\"id\":\"").Append(building.id).Append('"');
			sb.Append(",\"x\":").Append(building.transform.position.x.ToString("R", inv));
			sb.Append(",\"y\":").Append(building.transform.position.y.ToString("R", inv));
			sb.Append(",\"health\":").Append(building.health.ToString("R", inv));
			sb.Append(",\"hasTable\":").Append(hasTable ? "true" : "false").Append('}');
		}
		sb.Append("]}");
		listed.Add(sb.ToString());
	}
	return "{\"ok\":true,\"total\":" + all.Length.ToString(inv)
		+ ",\"requireGround\":" + requireGround.ToString(inv)
		+ ",\"multiCells\":" + multiCells.ToString(inv)
		+ ",\"listed\":" + listed.Count.ToString(inv)
		+ ",\"entries\":[" + string.Join(",", listed) + "]}";
}))()
