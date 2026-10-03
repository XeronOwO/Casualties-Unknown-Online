// recipe: item-world-read
// args: x=n y=n radius=n
// serves: item-creation-registration-first rows 2 and 4 (the refused drop ids, and the world item two senders contend for)
// returns: ok, cellX, cellY, radius, count, items, error, detail
//
// Lists the world items (not inside any body) within <radius> world units of the absolute cell
// (x, y), each with its readable ItemInstanceId; <radius> <= 0 means the 45-unit default and the
// returned radius field is always the effective one. Read-only: the run uses it to learn the id a
// later operation must name, and to read one world item from several clients. No inner delegate
// captures a local: the evaluator's Mono REPL refuses that shape, so the component scan is inlined.
// One eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\",\"detail\":\"no world is loaded\"}"; }
	var cell = new Vector2Int((int)({{n:x}}), (int)({{n:y}}));
	var radius = (float)({{n:radius}});
	if (radius <= 0f) { radius = 45f; }
	var center = world.BlockToWorldPos(cell);
	var found = UnityEngine.Object.FindObjectsOfType(typeof(Item));
	var sb = new System.Text.StringBuilder();
	var count = 0;
	for (var i = 0; i < found.Length; i++) {
		var item = found[i] as Item;
		if (item == null || item.GetComponentInParent<Body>() != null) { continue; }
		var pos = item.transform.position;
		var dx = pos.x - center.x;
		var dy = pos.y - center.y;
		if (dx * dx + dy * dy > radius * radius) { continue; }
		var itemId = 0UL;
		var components = item.GetComponents<UnityEngine.Component>();
		for (var c = 0; c < components.Length; c++) {
			if (components[c] == null || components[c].GetType().Name != "ItemInstanceId") { continue; }
			var idField = components[c].GetType().GetField("Id", flags);
			if (idField != null) { itemId = System.Convert.ToUInt64(idField.GetValue(components[c])); }
			else {
				var idProperty = components[c].GetType().GetProperty("Id", flags);
				if (idProperty != null) { itemId = System.Convert.ToUInt64(idProperty.GetValue(components[c], null)); }
			}
			break;
		}
		if (count > 0) { sb.Append(','); }
		count = count + 1;
		sb.Append("{\"id\":\"").Append(itemId.ToString(inv)).Append('"');
		sb.Append(",\"type\":\"").Append(item.id).Append('"');
		sb.Append(",\"hasId\":").Append(itemId != 0UL ? "true" : "false");
		sb.Append(",\"fresh\":").Append(item.GetComponent("FreshItemDrop") != null ? "true" : "false");
		sb.Append(",\"x\":").Append(pos.x.ToString("0.###", inv)).Append(",\"y\":").Append(pos.y.ToString("0.###", inv));
		sb.Append('}');
	}
	return "{\"ok\":true,\"cellX\":" + cell.x.ToString(inv) + ",\"cellY\":" + cell.y.ToString(inv)
		+ ",\"radius\":" + radius.ToString("0.###", inv)
		+ ",\"count\":" + count.ToString(inv)
		+ ",\"items\":[" + sb.ToString() + "]}";
}))()
