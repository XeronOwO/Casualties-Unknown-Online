// recipe: item-customize-native
// args: slot=n label=s swatch=n
// serves: 1.0-item-customization-truth
// returns: ok, id, label, swatch, error
//
// The player's customizer path: open the native item customizer on a carried item,
// write the label and colour, then close it through the game's own close entry. The
// close is the event that republishes the local character snapshot before a save.
((System.Func<string>)(() => {
	var slotArgument = {{n:slot}};
	var label = {{s:label}};
	var swatch = {{n:swatch}};
	var camera = PlayerCamera.main;
	var body = camera != null ? camera.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var item = body.GetItem(slotArgument);
	if (item == null) { return "{\"ok\":false,\"error\":\"no-item\"}"; }
	camera.OpenItemCustomizer(item);
	item.label = label;
	item.colorSwatch = swatch;
	camera.CloseItemCustomizer();
	return "{\"ok\":true,\"id\":\"" + item.id + "\",\"label\":\"" + (item.label ?? "") + "\",\"swatch\":" + item.colorSwatch + "}";
}))()
