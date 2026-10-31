// recipe: wound-seed
// args: limb=n force=n
// returns: ok, limb, skinHealth, bleedAmount, pain, hasShrapnel, error, detail
//
// Puts a REAL wound on one of the caller's own limbs through the game's own damage entry — `Limb.ImpactDamage(force, dir)`, the
// function a fall, a spike or a hit runs (reversing/Assembly-CSharp/Assembly-CSharp/Limb.cs: the method subtracts skinHealth by
// force * 0.33 / armorReduction and feeds the bleed fields). It exists because the medical rows need a wounded patient and this
// machine's generated worlds do not produce one on demand; a fixture that calls the game's own damage function is the honest
// stand-in, and the fields it writes are the ones the acceptance reads (`limb-read`).
((System.Func<string>)(() => {
	var limbIndex = (int){{n:limb}};
	var force = (float)({{n:force}});
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	if (limbIndex < 0 || limbIndex >= body.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\"}"; }
	var limb = body.limbs[limbIndex];
	if (limb == null) { return "{\"ok\":false,\"error\":\"no-such-limb\"}"; }

	var before = limb.skinHealth;
	limb.ImpactDamage(force, new Vector2(0f, -1f));
	var after = limb.skinHealth;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	return "{\"ok\":true,\"limb\":" + limbIndex
		+ ",\"skinBefore\":" + before.ToString("0.###", inv) + ",\"skinHealth\":" + after.ToString("0.###", inv)
		+ ",\"bleedAmount\":" + limb.bleedAmount.ToString("0.###", inv)
		+ ",\"pain\":" + limb.pain.ToString("0.###", inv)
		+ ",\"hasShrapnel\":" + (limb.hasShrapnel ? "true" : "false") + "}";
}))()
