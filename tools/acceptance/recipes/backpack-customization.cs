// recipe: backpack-customization
// args: none
// serves: 1.0-item-customization-truth
// returns: ok, count, items (id, label, swatch per displayed-backpack item)
//
// The displayed backpack's own items: every item in the scene that carries the
// remote display marker, with the label and colour the display built. This is
// the copy a teammate sees when they open the owner's backpack.
((System.Func<string>)(() => {
	var markerType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteInventoryItemId, CasualtiesUnknownOnline.GameAdapter", false);
	if (markerType == null) { return "{\"ok\":false,\"error\":\"no-marker-type\"}"; }
	var rows = new System.Collections.Generic.List<string>();
	foreach (var item in Item.allItems) {
		if (item == null) { continue; }
		if (item.GetComponent(markerType) == null) { continue; }
		rows.Add("{\"id\":\"" + item.id + "\",\"label\":\"" + (item.label ?? "") + "\",\"swatch\":" + item.colorSwatch + "}");
	}
	return "{\"ok\":true,\"count\":" + rows.Count + ",\"items\":[" + string.Join(",", rows) + "]}";
}))()
