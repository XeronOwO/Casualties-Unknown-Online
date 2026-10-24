// recipe: building-health-set
// args: x=n y=n health=n
// serves: host-blast-rolls-a-second-drop-set (its count row needs a shared carrier to die through its OWN path)
// returns: ok, x, y, healthBefore, healthAfter, error, detail
//
// Sets one building's health to the given value — the state a player's damage leaves it in. A turret below 350 starts its
// OWN two-second countdown (`TurretScript`), which ends in `WorldGeneration.CreateExplosion` and therefore in the carrier
// event and that carrier's native drop roll, so the member-side half of the row runs through the game's own code with only
// the damage replaced. Writing 0 instead skips the countdown (the entity dies silently), which is why the value is the
// caller's.
//
// Found by a manual distance test: `Vector2.Distance` inside a recipe is one of the shapes this evaluator refuses as (1,1).
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = {{n:x}};
	var y = {{n:y}};
	var health = {{n:health}};
	var all = UnityEngine.Object.FindObjectsOfType<BuildingEntity>();
	var hit = false;
	var hitX = 0f;
	var hitY = 0f;
	var before = 0f;
	for (var i = 0; i < all.Length; i++) {
		var candidate = all[i];
		var pos = candidate.transform.position;
		var dx = pos.x - x;
		var dy = pos.y - y;
		if ((dx * dx) + (dy * dy) > 9f) { continue; }
		before = candidate.health;
		candidate.health = health;
		hit = true;
		hitX = pos.x;
		hitY = pos.y;
		break;
	}
	if (!hit) { return "{\"ok\":false,\"error\":\"no-building-near\"}"; }
	return "{\"ok\":true,\"x\":" + hitX.ToString("0.##", inv)
		+ ",\"y\":" + hitY.ToString("0.##", inv)
		+ ",\"healthBefore\":" + before.ToString("0.##", inv)
		+ ",\"healthAfter\":" + health.ToString("0.##", inv) + "}";
}))()
