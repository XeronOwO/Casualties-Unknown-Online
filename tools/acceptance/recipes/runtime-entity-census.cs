// recipe: runtime-entity-census
// args: none
// serves: runtime-entity-spawn-backfill rows 1-4, 11 and 12, runtime-entity-creation-rejection rows 1-5 and 7
// returns: ok, utc, marked, items
//
// Every live BuildingEntity carrying the runtime creation marker, with the FULL creation key (prefab id,
// creation cell, creator, sequence) and the copy's current position and health — the cross-client census
// the backfill and rejection rows read. Generation entities carry no marker and are never listed. Read-only.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var all = UnityEngine.Object.FindObjectsOfType<BuildingEntity>(true);
	var sb = new System.Text.StringBuilder();
	var n = 0;
	for (var i = 0; i < all.Length; i++) {
		var e = all[i];
		if (e == null) { continue; }
		var comp = e.GetComponent("CasualtiesUnknownOnline.GameAdapter.World.RuntimeEntityCreation");
		if (comp == null) { continue; }
		var t = comp.GetType();
		var markerId = t.GetProperty("Id", flags).GetValue(comp, null);
		var cellX = t.GetProperty("CellX", flags).GetValue(comp, null);
		var cellY = t.GetProperty("CellY", flags).GetValue(comp, null);
		var creator = t.GetProperty("CreatorSteamId", flags).GetValue(comp, null);
		var seq = t.GetProperty("CreationSequence", flags).GetValue(comp, null);
		var p = e.transform.position;
		if (n > 0) { sb.Append(','); }
		n = n + 1;
		sb.Append("{\"seq\":").Append(System.Convert.ToString(seq, inv))
			.Append(",\"id\":\"").Append(System.Convert.ToString(markerId)).Append('"')
			.Append(",\"cellX\":").Append(System.Convert.ToString(cellX, inv))
			.Append(",\"cellY\":").Append(System.Convert.ToString(cellY, inv))
			.Append(",\"creator\":\"").Append(System.Convert.ToString(creator, inv)).Append('"')
			.Append(",\"animal\":").Append(e.animal ? "true" : "false")
			.Append(",\"x\":").Append(p.x.ToString("0.000", inv)).Append(",\"y\":").Append(p.y.ToString("0.000", inv))
			.Append(",\"health\":").Append(e.health.ToString("0.###", inv)).Append('}');
	}
	return "{\"ok\":true,\"utc\":\"" + System.DateTime.UtcNow.ToString("o") + "\",\"marked\":" + n.ToString(inv)
		+ ",\"items\":[" + sb.ToString() + "]}";
}))()
