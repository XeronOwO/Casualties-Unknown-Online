// recipe: limb-infect
// args: value=n
// returns: ok, limb, name, infection, dismembered, error
//
// Raises the SELECTED limb's infection, the one field the amputation gate reads: `Item.DoAmputate` starts its minigame only
// when `limb.infectionAmount > 60f` and the limb is not a torso or the head (Item.cs:7127-7142). A fixture write to the game's
// own field, so the amputation row can be reached without waiting for a real infection.
((System.Func<string>)(() => {
	var value = (float){{n:value}};
	var camera = PlayerCamera.main;
	if (camera == null || camera.selectedLimb == null) { return "{\"ok\":false,\"error\":\"no-selected-limb\"}"; }
	var limb = camera.selectedLimb;
	limb.infectionAmount = value;
	return "{\"ok\":true,\"name\":\"" + limb.fullName + "\",\"infection\":" + limb.infectionAmount.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ",\"dismembered\":" + (limb.dismembered ? "true" : "false") + "}";
}))()