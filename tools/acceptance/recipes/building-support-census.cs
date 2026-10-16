// recipe: building-support-census
// args: limit=n tables=n
// serves: validation-and-source-checks S2b — the question its acceptance record left open: does THIS map
//         carry a requireGround building at all, which support cell does one stand on, and can that
//         building's own prefab table account for a drop? The captured-rows half of the bound is readable
//         only where both answers are yes.
// returns: ok, total, requireGround, withTable, tableless, marked, listed, entries (each id, position,
//          blockPlacedOn, health and its authored drop table: always, gated, categories, the pool union and
//          the multiplier), error
//
// A building dies of a lost support block only when its own prefab sets requireGround: world generation
// registers that entity's own CheckSeating on the support cell's chunk (the game's DistributeEntities
// path), and the zeroed health is what makes its next Update roll the drops. The host's support-loss
// capture reads exactly that population off the live components, so this census is the precondition of the
// run that reads the captured-rows half: it names a building AND the cell to break under it, and the table
// a break report for that cell would be judged against. The list is a SUPERSET of what the capture reads —
// the capture additionally skips an entity that already carries the remote-death mark, which is what the
// per-entry `marked` field is for (a marked entity contributes no rows, so a cell whose only building is
// marked cannot produce the reading). tables=1 lists only the entities that CARRY authored drop data at
// all (any of the three buckets non-empty) — the subpopulation a reading of the bound is possible on; the
// counts stay population-wide, so a run reads "this map has N requireGround buildings and M of them carry
// a table" from one call. Which ROWS a table yields is still the bound's own rule (GameBuildingDropTable),
// confirmed by the host's capture line rather than re-derived here. Run it on the client whose world is
// judged (the host) and on the one that breaks (the member) — two lists naming the same cells is also the
// reading that the population itself is shared. Read-only.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var limit = (int)({{n:limit}});
	var tables = (int)({{n:tables}});
	var all = UnityEngine.Object.FindObjectsOfType<BuildingEntity>();
	if (all == null) { return "{\"ok\":false,\"error\":\"no-census\"}"; }
	var listed = new System.Collections.Generic.List<string>();
	var requireGround = 0;
	var withTable = 0;
	var marked = 0;
	foreach (var building in all) {
		if (building == null || !building.requireGround) { continue; }
		requireGround++;
		var isMarked = building.GetComponent<CasualtiesUnknownOnline.GameAdapter.World.RemoteEntityDeath>() != null;
		if (isMarked) { marked++; }
		var hasTable = (building.alwaysDrop != null && building.alwaysDrop.Length > 0)
			|| (building.itemsDropOnDestroy != null && building.itemsDropOnDestroy.Length > 0)
			|| building.guaranteedDropAmount > 0;
		if (hasTable) { withTable++; }
		if (tables != 0 && !hasTable) { continue; }
		if (listed.Count >= limit) { continue; }
		var pos = building.transform.position;
		var sb = new System.Text.StringBuilder();
		sb.Append("{\"id\":\"").Append(building.id).Append('"');
		sb.Append(",\"x\":").Append(pos.x.ToString("R", inv));
		sb.Append(",\"y\":").Append(pos.y.ToString("R", inv));
		sb.Append(",\"cellX\":").Append(building.blockPlacedOn.x.ToString(inv));
		sb.Append(",\"cellY\":").Append(building.blockPlacedOn.y.ToString(inv));
		sb.Append(",\"health\":").Append(building.health.ToString("R", inv));
		sb.Append(",\"marked\":").Append(isMarked ? "true" : "false");
		sb.Append(",\"multiplier\":").Append(building.dropChanceMultiplier.ToString("R", inv));
		sb.Append(",\"guaranteed\":").Append(building.guaranteedDropAmount.ToString(inv));
		sb.Append(",\"always\":[");
		if (building.alwaysDrop != null) {
			for (var i = 0; i < building.alwaysDrop.Length; i++) {
				if (i > 0) { sb.Append(','); }
				sb.Append("{\"id\":\"").Append(building.alwaysDrop[i].id).Append('"');
				sb.Append(",\"min\":").Append(building.alwaysDrop[i].conditionMin.ToString("R", inv));
				sb.Append(",\"max\":").Append(building.alwaysDrop[i].conditionMax.ToString("R", inv)).Append('}');
			}
		}
		sb.Append("],\"gated\":[");
		if (building.itemsDropOnDestroy != null) {
			for (var i = 0; i < building.itemsDropOnDestroy.Length; i++) {
				if (i > 0) { sb.Append(','); }
				sb.Append("{\"id\":\"").Append(building.itemsDropOnDestroy[i].id).Append('"');
				sb.Append(",\"chance\":").Append(building.itemsDropOnDestroy[i].chance.ToString("R", inv));
				sb.Append(",\"min\":").Append(building.itemsDropOnDestroy[i].conditionMin.ToString("R", inv));
				sb.Append(",\"max\":").Append(building.itemsDropOnDestroy[i].conditionMax.ToString("R", inv)).Append('}');
			}
		}
		sb.Append("],\"categories\":[");
		if (building.itemCategoriesToAdd != null) {
			for (var i = 0; i < building.itemCategoriesToAdd.Length; i++) {
				if (i > 0) { sb.Append(','); }
				sb.Append('"').Append(building.itemCategoriesToAdd[i]).Append('"');
			}
		}
		sb.Append("],\"pool\":[");
		var pool = ItemLootPool.pool;
		if (pool != null && building.itemCategoriesToAdd != null) {
			var ids = new System.Collections.Generic.List<string>();
			for (var c = 0; c < building.itemCategoriesToAdd.Length; c++) {
				var category = building.itemCategoriesToAdd[c];
				if (category == null) { continue; }
				System.Collections.Generic.List<string> entries;
				if (!pool.TryGetValue(category, out entries) || entries == null) { continue; }
				for (var e = 0; e < entries.Count; e++) {
					if (!string.IsNullOrEmpty(entries[e]) && !ids.Contains(entries[e])) { ids.Add(entries[e]); }
				}
			}
			for (var i = 0; i < ids.Count; i++) {
				if (i > 0) { sb.Append(','); }
				sb.Append('"').Append(ids[i]).Append('"');
			}
		}
		sb.Append("]}");
		listed.Add(sb.ToString());
	}
	return "{\"ok\":true,\"total\":" + all.Length.ToString(inv)
		+ ",\"requireGround\":" + requireGround.ToString(inv)
		+ ",\"withTable\":" + withTable.ToString(inv)
		+ ",\"tableless\":" + (requireGround - withTable).ToString(inv)
		+ ",\"marked\":" + marked.ToString(inv)
		+ ",\"listed\":" + listed.Count.ToString(inv)
		+ ",\"entries\":[" + string.Join(",", listed) + "]}";
}))()
