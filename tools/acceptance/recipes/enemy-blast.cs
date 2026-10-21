// recipe: enemy-blast
// args: prefab=s damage=n range=n
// serves: enemy-generation-pairing-late-join rows 3 and 6 (a controlled non-lethal hit and a kill/removal on a bound enemy), follow-up cycles that need one enemy's health to move without a weapon gesture
// returns: ok, prefab, iid, x, y, damage, range, healthBefore, healthAfter, bodyDistance, bodyInBlast, error, detail
//
// ONE structural-only explosion through the game's own WorldGeneration.CreateExplosion (the damage funnel the
// runtime-entity-kill recipe uses) at the position of the animal with the given prefab id nearest to this
// client's body. Run it on the HOST: the host's copy is the authoritative one, and on a member the next 20 Hz
// state batch rewrites the copy's health, so the hit is neither durable nor reported (the recipe refuses a
// non-host caller).
//
// The blast is the game's own explosion, not a single-target or body-safe probe: the structural damage is
// applied as requested * Random.Range(0, 2), so a requested damage below the animal's health can still kill it
// (read healthBefore/healthAfter; use well under half the health for a non-lethal hit); every building entity
// and item inside `range` is hit and the ground is carved with a block circle; and the LOCAL body inside
// 2.5 * range still gets the game's non-damage blast effects (consciousness 31, hearing loss, eye close/scare)
// — the answer reports bodyDistance/bodyInBlast so the caller can name that state. Setup/damage drive, not a
// read.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Host) {
		return "{\"ok\":false,\"error\":\"host-only\",\"detail\":\"run the blast on the host; a member's copy is overwritten by the next 20 Hz state batch\"}";
	}
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var prefab = {{s:prefab}};
	var damage = (float)({{n:damage}});
	var range = (float)({{n:range}});
	if (damage < 0f || range <= 0f) {
		return "{\"ok\":false,\"error\":\"bad-args\",\"detail\":\"damage must be >= 0 and range > 0\"}";
	}
	var quotedPrefab = prefab.Replace("\\", "\\\\").Replace("\"", "\\\"");
	var bx = body.transform.position.x;
	var by = body.transform.position.y;
	var animals = UnityEngine.Object.FindObjectsOfType<BuildingEntity>(true);
	BuildingEntity target = null;
	var best = float.MaxValue;
	for (var i = 0; i < animals.Length; i++) {
		var e = animals[i];
		if (e == null || !e.animal) { continue; }
		if (!System.String.Equals(e.id, prefab, System.StringComparison.Ordinal)) { continue; }
		var dx = e.transform.position.x - bx;
		var dy = e.transform.position.y - by;
		var d = (float)System.Math.Sqrt(dx * dx + dy * dy);
		if (d < best) { best = d; target = e; }
	}
	if (target == null) { return "{\"ok\":false,\"error\":\"no-target\",\"detail\":\"no animal with prefab " + quotedPrefab + "\"}"; }
	var p = target.transform.position;
	var bodyDistance = (float)System.Math.Sqrt((p.x - bx) * (p.x - bx) + (p.y - by) * (p.y - by));
	var bodyInBlast = bodyDistance < range * 2.5f;
	var before = target.health;
	var x = new ExplosionParams();
	x.position = p;
	x.muscleDamage = new RangeF(0f, 0f);
	x.skinDamage = new RangeF(0f, 0f);
	x.skinDamageChance = 0f;
	x.boneBreakChance = 0f;
	x.dislocationChance = 0f;
	x.disfigureChance = 0f;
	x.bleedChance = 0f;
	x.bleedAmount = new RangeF(0f, 0f);
	x.structuralDamage = damage;
	x.range = range;
	x.velocity = 0f;
	x.shrapnelChance = 0f;
	WorldGeneration.CreateExplosion(x);
	var after = target == null ? -1f : target.health;
	return "{\"ok\":true,\"prefab\":\"" + quotedPrefab + "\""
		+ ",\"iid\":" + target.GetInstanceID().ToString(inv)
		+ ",\"x\":" + p.x.ToString("0.000", inv) + ",\"y\":" + p.y.ToString("0.000", inv)
		+ ",\"damage\":" + damage.ToString("0.###", inv) + ",\"range\":" + range.ToString("0.###", inv)
		+ ",\"bodyDistance\":" + bodyDistance.ToString("0.###", inv)
		+ ",\"bodyInBlast\":" + (bodyInBlast ? "true" : "false")
		+ ",\"healthBefore\":" + before.ToString("0.###", inv)
		+ ",\"healthAfter\":" + after.ToString("0.###", inv) + "}";
}))()
