// recipe: saved-character-file
// args: guest=s
// returns: ok, found, count, items (id/label/swatch per stored item)
//
// Reads the HOST's newest world's live.cuoz: the archive is a zip and the
// characters live inside it as characters/<playerKey>.json (the world folder
// itself has no characters directory). The IP-direct key is name-<display>, not
// steam-<id>, so the reader unions every character record in the archive.
((System.Func<string>)(() => {
	var raw = {{s:guest}};
	var root = System.Environment.GetEnvironmentVariable("CUO_SAVE_ROOT");
	if (string.IsNullOrEmpty(root)) { return "{\"ok\":false,\"error\":\"no-save-root\"}"; }
	string world = null;
	var newest = System.DateTime.MinValue;
	foreach (var dir in System.IO.Directory.GetDirectories(root)) {
		var liveFile = System.IO.Path.Combine(dir, "live.cuoz");
		if (!System.IO.File.Exists(liveFile)) { continue; }
		var stamp = System.IO.File.GetLastWriteTimeUtc(liveFile);
		if (stamp > newest) { newest = stamp; world = dir; }
	}
	if (world == null) { return "{\"ok\":true,\"found\":false}"; }
	var live = System.IO.Path.Combine(world, "live.cuoz");
	var records = new System.Text.StringBuilder();
	var count = 0;
	using (var zip = System.IO.Compression.ZipFile.OpenRead(live)) {
		foreach (var entry in zip.Entries) {
			if (!entry.FullName.StartsWith("characters/", System.StringComparison.Ordinal) || !entry.FullName.EndsWith(".json", System.StringComparison.Ordinal)) { continue; }
			using (var reader = new System.IO.StreamReader(entry.Open())) {
				using (var doc = System.Text.Json.JsonDocument.Parse(reader.ReadToEnd())) {
					var top = doc.RootElement;
					System.Text.Json.JsonElement character;
					if (top.ValueKind == System.Text.Json.JsonValueKind.Array) {
						if (top.GetArrayLength() == 0) { continue; }
						character = top[0];
					} else {
						character = top;
					}
					if (character.ValueKind != System.Text.Json.JsonValueKind.Object || !character.TryGetProperty("items", out var items) || items.ValueKind != System.Text.Json.JsonValueKind.Array) { continue; }
					var stack = new System.Collections.Generic.Stack<System.Text.Json.JsonElement>();
					for (var i = items.GetArrayLength() - 1; i >= 0; i--) { stack.Push(items[i]); }
					while (stack.Count > 0) {
						var item = stack.Pop();
						if (count > 0) { records.Append(','); }
						var itemId = item.TryGetProperty("itemId", out var iid) && iid.ValueKind == System.Text.Json.JsonValueKind.String ? iid.GetString() : "";
						var label = item.TryGetProperty("label", out var lbl) && lbl.ValueKind == System.Text.Json.JsonValueKind.String ? lbl.GetString() : "";
						var swatch = item.TryGetProperty("colorSwatch", out var sw) && sw.ValueKind == System.Text.Json.JsonValueKind.Number ? sw.GetInt32() : 0;
						records.Append("{\"id\":\"").Append((itemId ?? "").Replace("\"", "'")).Append("\",\"label\":\"").Append((label ?? "").Replace("\"", "'")).Append("\",\"swatch\":").Append(swatch).Append('}');
						count++;
						if (item.TryGetProperty("contents", out var contents) && contents.ValueKind == System.Text.Json.JsonValueKind.Array) {
							for (var c = contents.GetArrayLength() - 1; c >= 0; c--) { stack.Push(contents[c]); }
						}
					}
				}
			}
		}
	}
	return "{\"ok\":true,\"found\":true,\"count\":" + count + ",\"items\":[" + records + "]}";
}))()
