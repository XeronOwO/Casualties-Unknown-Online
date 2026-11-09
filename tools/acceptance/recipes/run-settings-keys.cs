// recipe: run-settings-keys
// args: none
// returns: ok, count, keys
//
// The run settings the game's own `WorldGeneration.runSettings` holds in this run: every key and
// value the host's baseline snapshot will carry. A 1.0-added setting appears here or the snapshot
// cannot carry it.
((System.Func<string>)(() => {
	var field = typeof(WorldGeneration).GetField("runSettings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	var settings = field == null ? null : field.GetValue(null) as System.Collections.Generic.Dictionary<string, object>;
	if (settings == null) { return "{\"ok\":false,\"error\":\"no-run-settings\"}"; }
	var rows = new System.Collections.Generic.List<string>();
	foreach (var pair in settings) { rows.Add(pair.Key + "=" + (pair.Value == null ? "null" : pair.Value.ToString())); }
	rows.Sort(string.CompareOrdinal);
	return "{\"ok\":true,\"count\":" + rows.Count + ",\"keys\":\"" + string.Join(";", rows).Replace("\"", "'") + "\"}";
}))()
