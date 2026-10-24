// recipe: building-destroy-at
// args: x=n y=n
// serves: host-blast-rolls-a-second-drop-set (its count row needs a shared building's death)
// returns: ok, x, y, healthBefore, error, detail
//
// Drops the health of a building AT the given point to zero, which is the state the game's own death branch reads: the
// building dies on THIS client, rolls its own drops and reports its carrier event through the ordinary patch. The write is
// the state a destroyed building ends in (`BuildingEntity.health`), so the member-side half of the row runs without a
// gesture the driver cannot make. Found by a manual distance test: a `Vector2.Distance` helper inside a recipe is one of
// the shapes this evaluator refuses as (1,1).
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = {{n:x}};
	var y = {{n:y}};
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
		candidate.health = 0f;
		hit = true;
		hitX = pos.x;
		hitY = pos.y;
		break;
	}
	if (!hit) { return "{\"ok\":false,\"error\":\"no-building-near\"}"; }
	return "{\"ok\":true,\"x\":" + hitX.ToString("0.##", inv)
		+ ",\"y\":" + hitY.ToString("0.##", inv)
		+ ",\"healthBefore\":" + before.ToString("0.##", inv) + "}";
}))()
