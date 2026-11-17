// recipe: geyser-types
// args: none
// serves: save-continue — the geyser type table the host's world holds, active or not
// returns: ok, total, started, rows (x, y, type, started), error
//
// Reads every GeyserScript in THIS client's scene, including inactive chunks. `type` is the
// decided liquidType; `started` is the geyser's own Start-ran mark (native Start sets `bld`).
// Read-only, one eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var all = UnityEngine.Object.FindObjectsOfType<GeyserScript>(true);
	var sb = new System.Text.StringBuilder();
	var started = 0;
	for (var i = 0; i < all.Length; i++) {
		var g = all[i];
		if (g == null) { continue; }
		var typeField = g.GetType().GetField("liquidType", flags);
		var type = typeField == null ? -1 : System.Convert.ToInt32(typeField.GetValue(g), inv);
		var bldField = g.GetType().GetField("bld", flags);
		var isStarted = bldField != null && bldField.GetValue(g) != null;
		if (isStarted) { started++; }
		var pos = g.transform.position;
		if (i > 0) { sb.Append(','); }
		sb.Append("{\"x\":").Append(pos.x.ToString("0.###", inv))
			.Append(",\"y\":").Append(pos.y.ToString("0.###", inv))
			.Append(",\"type\":").Append(type.ToString(inv))
			.Append(",\"started\":").Append(isStarted ? "true" : "false").Append('}');
	}
	return "{\"ok\":true,\"total\":" + all.Length.ToString(inv) + ",\"started\":" + started.ToString(inv)
		+ ",\"rows\":[" + sb.ToString() + "]}";
}))()
