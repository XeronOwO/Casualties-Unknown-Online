// recipe: world-customization
// args: none
// serves: 1.0-item-customization-truth
// returns: ok, count, items (id, label, swatch, x, y per customized item in the scene)
//
// Every item in the scene whose label or colour swatch is set, wherever it lives.
// Used on the second client to read the copy of an item the host customized and
// dropped: the item crossed a boundary, so the copy's own values are the wire's.
((System.Func<string>)(() => {
	var rows = new System.Collections.Generic.List<string>();
	foreach (var item in Item.allItems) {
		if (item == null) { continue; }
		var label = item.label ?? "";
		if (label.Length == 0 && item.colorSwatch == 0) { continue; }
		var position = item.transform.position;
		rows.Add("{\"id\":\"" + item.id + "\",\"label\":\"" + label + "\",\"swatch\":" + item.colorSwatch
			+ ",\"x\":" + position.x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"y\":" + position.y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "}");
	}
	return "{\"ok\":true,\"count\":" + rows.Count + ",\"items\":[" + string.Join(",", rows) + "]}";
}))()
