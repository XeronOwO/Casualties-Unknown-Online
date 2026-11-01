// recipe: select-limb
// args: limb=n
// returns: ok, limb, name, health, error, detail
//
// Sets the limb the game's own wound view treats, by writing `PlayerCamera.main.selectedLimb` - the field the view writes when
// the player hovers a limb. It is a SETUP substitution, not a player entry: the row it serves tests the gore clip reaching a
// peer, and the limb selection is the precondition. The pointer route cannot drive it reliably (the view reads hover, and a
// staged pointer that teleports between frames does not hover), which is recorded in the ticket. One top-level expression.
((System.Func<string>)(() => { var inv = System.Globalization.CultureInfo.InvariantCulture; var index = (int){{n:limb}}; var camera = PlayerCamera.main; if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; } var body = camera.body; if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; } if (index < 0 || index >= body.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\"}"; } camera.selectedLimb = body.limbs[index]; var limb = camera.selectedLimb; return "{\"ok\":true,\"limb\":" + index.ToString(inv) + ",\"name\":\"" + limb.name + "\",\"health\":" + limb.skinHealth.ToString("0.#", inv) + "}"; }))()