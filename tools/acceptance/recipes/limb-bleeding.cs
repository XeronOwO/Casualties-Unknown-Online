// recipe: limb-bleeding
// args: limb=n
// returns: ok, limbs, lower, limb, error
//
// Reads every limb of the LOCAL body with the facts the tourniquet and splint rows turn
// on: `broken` and `splinted` (the game's own flags) plus whether the limb carries the
// item's component (`TourniquetScript`, `SplintLimb`). The two facts a tourniquet row turns on:
// `blockedBleeding` (the flag `TourniquetScript.Start` sets on its limb and on every
// limb `GetLowerLimbs` returns, and clears in `TakeOff`) and whether the limb carries a
// `TourniquetScript` at all. `lower` is the game's own answer for the requested limb:
// `TourniquetScript.GetLowerLimbs(limb, null)` mapped to limb indices, so the row asserts
// the game's set rather than a guess about anatomy (docs/design/mechanics/medical-view.md:
// "A tourniquet blocks the limb it is on and every limb further from the heart").
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var requested = {{n:limb}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-player-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }

	var limbs = body.limbs;
	var sb = new System.Text.StringBuilder();
	sb.Append("{\"ok\":true,\"limbs\":[");
	for (var i = 0; i < limbs.Length; i++) {
		var limb = limbs[i];
		if (limb == null) { continue; }
		if (i > 0) { sb.Append(','); }
		sb.Append("{\"i\":").Append(i.ToString(inv));
		sb.Append(",\"name\":\"").Append(limb.fullName).Append('"');
		sb.Append(",\"skin\":").Append(limb.skinHealth.ToString("0.###", inv));
		sb.Append(",\"bleed\":").Append(limb.bleedAmount.ToString("0.###", inv));
		sb.Append(",\"blocked\":").Append(limb.blockedBleeding ? "true" : "false");
		sb.Append(",\"tourniquet\":").Append(limb.GetComponent<TourniquetScript>() != null ? "true" : "false");
		sb.Append(",\"dismembered\":").Append(limb.dismembered ? "true" : "false");
		sb.Append(",\"broken\":").Append(limb.broken ? "true" : "false");
		sb.Append(",\"splinted\":").Append(limb.splinted ? "true" : "false");
		sb.Append(",\"splint\":").Append(limb.GetComponent<SplintLimb>() != null ? "true" : "false");
		// The splint's own heal effect is a faster bone timer (`SplintLimb.Update` subtracts
		// `Limb.boneHealSpeed * 1.5` while worn and only the plain speed without it), so the number itself is
		// what a row must read to tell "the game heals a splinted break faster" from "the flag was set".
		sb.Append(",\"boneTimer\":").Append(limb.boneHealTimer.ToString("0.####", inv));
		sb.Append(",\"head\":").Append(limb.isHead ? "true" : "false");
		sb.Append(",\"vital\":").Append(limb.isVital ? "true" : "false");
		sb.Append('}');
	}
	sb.Append("],\"lower\":[");
	if (requested >= 0 && requested < limbs.Length && limbs[requested] != null) {
		var lower = TourniquetScript.GetLowerLimbs(limbs[requested], null);
		var first = true;
		for (var j = 0; j < lower.Count; j++) {
			var index = System.Array.IndexOf(limbs, lower[j]);
			if (index < 0) { continue; }
			if (!first) { sb.Append(','); }
			sb.Append(index.ToString(inv));
			first = false;
		}
	}
	sb.Append("],\"limb\":").Append(requested.ToString(inv));
	sb.Append('}');
	return sb.ToString();
}))()
