// recipe: runtime-entity-kill
// args: mode=s sequence=s
// serves: runtime-entity-creation-rejection rows 4 and 5
// returns: ok, mode, sequence, prefab, cellX, cellY, x, y, healthBefore, healthAfter, error, detail
//
// Removes ONE exact runtime creation by its marker's creation sequence, located by its stamped key —
// never by position. mode=explode rolls a structural-only explosion at the entity's own position (no
// muscle, skin, bleed or shrapnel damage), so the death funnel reports the death like any other death.
// mode=destroy destroys the GameObject directly and deliberately leaves the creation record standing:
// the copy is gone while the pending report is still outstanding, which is the shape "the rejection
// arrives after the local copy already died" needs.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var mode = {{s:mode}};
	var targetSequence = System.Convert.ToUInt32({{s:sequence}}, inv);
	var all = UnityEngine.Object.FindObjectsOfType<BuildingEntity>(true);
	BuildingEntity target = null;
	for (var i = 0; i < all.Length; i++) {
		var e = all[i];
		if (e == null) { continue; }
		var comp = e.GetComponent("CasualtiesUnknownOnline.GameAdapter.World.RuntimeEntityCreation");
		if (comp == null) { continue; }
		var seq = System.Convert.ToUInt32(comp.GetType().GetProperty("CreationSequence", flags).GetValue(comp, null));
		if (seq == targetSequence) { target = e; break; }
	}
	if (target == null) {
		return "{\"ok\":false,\"error\":\"no-target\",\"detail\":\"no runtime creation with sequence " + targetSequence.ToString(inv) + "\"}";
	}
	var comp2 = target.GetComponent("CasualtiesUnknownOnline.GameAdapter.World.RuntimeEntityCreation");
	var markerType = comp2.GetType();
	var prefab = System.Convert.ToString(markerType.GetProperty("Id", flags).GetValue(comp2, null));
	var cellX = System.Convert.ToString(markerType.GetProperty("CellX", flags).GetValue(comp2, null), inv);
	var cellY = System.Convert.ToString(markerType.GetProperty("CellY", flags).GetValue(comp2, null), inv);
	var before = target.health;
	var pos = target.transform.position;
	if (mode == "explode") {
		var p = new ExplosionParams();
		p.position = pos;
		p.muscleDamage = new RangeF(0f, 0f);
		p.skinDamage = new RangeF(0f, 0f);
		p.skinDamageChance = 0f;
		p.boneBreakChance = 0f;
		p.dislocationChance = 0f;
		p.disfigureChance = 0f;
		p.bleedChance = 0f;
		p.bleedAmount = new RangeF(0f, 0f);
		p.structuralDamage = 500f;
		p.range = 1.5f;
		p.velocity = 0f;
		p.shrapnelChance = 0f;
		WorldGeneration.CreateExplosion(p);
	} else if (mode == "destroy") {
		UnityEngine.Object.Destroy(target.gameObject);
	} else {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be explode or destroy\"}";
	}
	var after = (target == null || mode == "destroy") ? -1f : target.health;
	return "{\"ok\":true,\"mode\":\"" + mode + "\""
		+ ",\"sequence\":" + targetSequence.ToString(inv)
		+ ",\"prefab\":\"" + prefab + "\""
		+ ",\"cellX\":" + cellX + ",\"cellY\":" + cellY
		+ ",\"x\":" + pos.x.ToString("0.000", inv) + ",\"y\":" + pos.y.ToString("0.000", inv)
		+ ",\"healthBefore\":" + before.ToString("0.###", inv)
		+ ",\"healthAfter\":" + after.ToString("0.###", inv) + "}";
}))()
