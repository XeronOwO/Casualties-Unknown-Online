// recipe: customize-open
// args: slot=n
// serves: 1.0-rebuild-each-acceptance-row-from-the-game (row 9, item-customization)
// returns: ok, name, label, swatch, active, error
//
// Opens the game's own item customizer on a carried item, through `PlayerCamera.OpenItemCustomizer` — the function
// the customize key runs when the pointer is over a bag item (`PlayerCamera.cs:1917-1919`). The row uses this instead
// of writing `item.label`/`item.colorSwatch`, which is our own field write and proves nothing about the screen.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var slot = {{n:slot}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var item = body.GetItem(slot);
	if (item == null) { return "{\"ok\":false,\"error\":\"no-item-in-slot\"}"; }
	camera.OpenItemCustomizer(item);
	var active = camera.itemCustomizer != null && camera.itemCustomizer.activeSelf;
	return "{\"ok\":true,\"name\":\"" + (item.fullName ?? "") + "\",\"label\":\"" + (item.label ?? "") + "\",\"swatch\":" + item.colorSwatch.ToString(inv) + ",\"active\":" + (active ? "true" : "false") + "}";
}))()
