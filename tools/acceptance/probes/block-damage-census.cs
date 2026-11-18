// recipe: block-damage-census
// args: none
// serves: block-damage-table-capacity-alignment, guest-partial-block-damage-re-report
// returns: ok, tableCount, emitted, skippedNull, rows, error
//
// Reads THIS client's live damaged-cell table (the game's own WorldGeneration.world.blockDamages) in
// the game's own list order - oldest first - so a run can compare the host's, a guest's and a late
// joiner's sets against each other and read which rows an eviction dropped. `tableCount` is the list's
// own count and `emitted` is how many rows this answer carries (they agree unless a null row was
// skipped), so a set comparison cannot mistake one number for the other. The game's cap is a literal
// inside its own method (WorldGeneration.cs:732) and is deliberately not restated here. Read-only, one eval.
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var rows = world.blockDamages;
	var sb = new System.Text.StringBuilder();
	var emitted = 0;
	var skippedNull = 0;
	for (var i = 0; i < rows.Count; i++) {
		var row = rows[i];
		if (row == null) { skippedNull++; continue; }
		if (emitted > 0) { sb.Append(','); }
		emitted++;
		sb.Append("{\"x\":").Append(row.pos.x.ToString(inv)).Append(",\"y\":").Append(row.pos.y.ToString(inv))
			.Append(",\"damage\":").Append(row.damage.ToString("0.###", inv)).Append('}');
	}
	return "{\"ok\":true,\"tableCount\":" + rows.Count.ToString(inv) + ",\"emitted\":" + emitted.ToString(inv)
		+ ",\"skippedNull\":" + skippedNull.ToString(inv) + ",\"rows\":[" + sb.ToString() + "]}";
}))()
