// recipe: limb-dismember
// args: limb=n mode=s
// serves: row 25 (prosthetic) — a real stump, made by the game's own amputation entry
// returns: ok, mode, limb, fullName, dismemberedBefore, dismemberedAfter, connected, error, detail
//
// `mode=make` runs `Limb.Dismember()` (the game's own amputation entry, `Limb.cs:127`), which is what the wound
// view's amputate button reaches; `mode=read` only reports the limb's own state. The parent limb is reported by
// index so the row can fit a prosthetic on the limb that actually carries the stump.
((System.Func<string>)(() => {
	var limbIndex = (int)({{n:limb}});
	var mode = {{s:mode}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	if (limbIndex < 0 || limbIndex >= body.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\",\"detail\":\"limb " + limbIndex + " of " + body.limbs.Length + "\"}"; }
	var limb = body.limbs[limbIndex];
	if (limb == null) { return "{\"ok\":false,\"error\":\"no-such-limb\"}"; }
	var before = limb.dismembered;
	var connected = "";
	if (limb.connectedLimbs != null) {
		for (var i = 0; i < limb.connectedLimbs.Length; i++) {
			var other = limb.connectedLimbs[i];
			if (other == null) { continue; }
			var index = System.Array.IndexOf(body.limbs.ToArray(), other);
			if (connected.Length > 0) { connected += ","; }
			connected += index.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
	}
	if (mode == "make" && !limb.dismembered) {
		limb.Dismember();
	}
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"limb\":" + limbIndex
		+ ",\"fullName\":\"" + limb.fullName + "\""
		+ ",\"dismemberedBefore\":" + (before ? "true" : "false")
		+ ",\"dismemberedAfter\":" + (limb.dismembered ? "true" : "false")
		+ ",\"connected\":\"" + connected + "\"}";
}))()
