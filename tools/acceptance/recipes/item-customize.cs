// recipe: item-customize
// args: slot=n label=s swatch=n
// serves: 1.0-item-customization-truth
// returns: ok, id, label, swatch, error
//
// Sets the two customization values the 1.0 item surface lets a player own --
// the label and the colour swatch -- on one carried item of the local body,
// through the same fields the game's customizer writes. The fixture stages the
// owner-side state; what travels is what this test reads back.
((System.Func<string>)(() => {
	var slotArgument = {{n:slot}};
	var label = {{s:label}};
	var swatch = {{n:swatch}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var item = body.GetItem(slotArgument);
	if (item == null) { return "{\"ok\":false,\"error\":\"no-item\"}"; }
	item.label = label;
	item.colorSwatch = swatch;
	return "{\"ok\":true,\"id\":\"" + item.id + "\",\"label\":\"" + (item.label ?? "") + "\",\"swatch\":" + item.colorSwatch + "}";
}))()
