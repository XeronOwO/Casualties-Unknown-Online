// recipe: limb-break
// args: limb=n
// returns: ok, limb, name, broken, skinHealth, bleedAmount, pain, error, detail
//
// Breaks one limb through the game's own `Limb.BreakBone()` (Limb.cs: `broken = true`, `skinHealth -= 33`, `bleedAmount +=
// Random.Range(0f, 20f)`). It exists because the splint family needs a BROKEN limb, and a break is the one wound the world does not
// hand out on demand. The treatment that follows stays the native path under test; this only creates the state it acts on.
((System.Func<string>)(() => {
	var index = (int){{n:limb}};
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var limbs = camera.body.limbs;
	if (index < 0 || index >= limbs.Length || limbs[index] == null) { return "{\"ok\":false,\"error\":\"bad-limb\"}"; }
	var limb = limbs[index];
	limb.BreakBone();
	return "{\"ok\":true,\"limb\":" + index + ",\"name\":\"" + limb.fullName + "\",\"broken\":" + (limb.broken ? "true" : "false")
		+ ",\"skinHealth\":" + limb.skinHealth.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"bleedAmount\":" + limb.bleedAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"pain\":" + limb.pain.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
