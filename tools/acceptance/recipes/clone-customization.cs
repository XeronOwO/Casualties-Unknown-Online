// recipe: clone-customization
// args: none
// serves: 1.0-item-customization-truth
// returns: ok, count, items (id, label, swatch per rendered clone item)
//
// The remote side of the customization reading: every item the carried-inventory
// renderer drew as a display clone, with the label and colour the clone carries.
// The host sets an item's values; the guest's copy must show them here. The
// clone marker is internal to the adapter, so the type is resolved by name.
((System.Func<string>)(() => {
	var cloneType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteCloneRender, CasualtiesUnknownOnline.GameAdapter", false);
	if (cloneType == null) { return "{\"ok\":false,\"error\":\"no-clone-type\"}"; }
	var clones = UnityEngine.Object.FindObjectsOfType(cloneType);
	var rows = new System.Collections.Generic.List<string>();
	foreach (var clone in clones) {
		var item = ((UnityEngine.Component)clone).GetComponent(typeof(Item)) as Item;
		if (item == null) { continue; }
		rows.Add("{\"id\":\"" + item.id + "\",\"label\":\"" + (item.label ?? "") + "\",\"swatch\":" + item.colorSwatch + "}");
	}
	return "{\"ok\":true,\"count\":" + rows.Count + ",\"items\":[" + string.Join(",", rows) + "]}";
}))()
