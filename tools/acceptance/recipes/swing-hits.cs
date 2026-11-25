// recipe: swing-hits
// args: none
// returns: ok, bodyX, bodyY, lookX, lookY, dirX, dirY, fromX, fromY, count, hits (name, tag, layer, distance,
//          pointX, pointY, damageCellX, damageCellY)
//
// Lists what an unarmed swing would meet, along the exact ray the game uses: from the attacking limb
// (`limbs[1]`) through the aim direction for the attack's own reach (`Body.cs:2162`, `RaycastAll` then only
// `BlockGround` takes damage at `:2166`). The game's block branch runs only when no earlier hit took the swing
// (`Body.cs:2160`), so a list that names an item or an entity before the block says what absorbed it.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var at = body.transform.position;
	var look = body.targetLookPos;
	var dx = look.x - at.x;
	var dy = look.y - at.y;
	var length = Math.Sqrt((dx * dx) + (dy * dy));
	if (length < 0.0001) { return "{\"ok\":false,\"error\":\"aim-has-no-direction\"}"; }
	var dir = new Vector2((float)(dx / length), (float)(dy / length));
	var from = body.limbs[1].transform.position;
	var reach = 4.5f;
	var text = "";
	var count = 0;
	foreach (var hit in Physics2D.RaycastAll(from, dir, reach))
	{
		// The game skips the attacker's own transform in this walk (`Body.cs:1919`: `if (raycastHit2D.transform !=
		// base.transform)`), so the player's own body is never an "earlier hit" that absorbed the swing. Listing it
		// made a row that aimed from inside its own cell read a Body hit and conclude the swing met nothing
		// (measured 2026-11-25: world-fingerprint, hits[0] = Body/Player, the block behind it never named).
		if (hit.transform == null || hit.transform == body.transform) { continue; }
		count++;
		if (count > 12) { break; }
		// The game does not damage the object it hit, it damages the CELL the hit point falls in:
		// `WorldGeneration.cs:655` maps a world position to a block, and `Body.cs:2162-2168` passes
		// `raycastHit2D.point + vector * 0.05f` through it. A `Chunk` collider covers many cells, so the object's own
		// position says nothing about the cell that loses damage (measured 2026-11-25: the row held the attack on the
		// cell its crosshair named while the hit point stood one cell over, and the damage read -1 there).
		//
		// The point comes back in WORLD units, the space `block-find-breakable` and `block-probe` answer in; a block
		// index (`WorldToBlockPos` adds the world's half width) is not a cell any other recipe accepts.
		var damaged = "";
		var hitPoint = hit.point + (dir * 0.05f);
		damaged = ",\"damageWorldX\":" + hitPoint.x.ToString("0.###", inv) + ",\"damageWorldY\":" + hitPoint.y.ToString("0.###", inv);
		text = text + (count > 1 ? "," : "") + "{\"name\":\"" + hit.transform.name.Replace("\"", "''") + "\",\"tag\":\"" + hit.transform.tag + "\",\"layer\":\"" + LayerMask.LayerToName(hit.transform.gameObject.layer) + "\",\"distance\":" + hit.distance.ToString("0.###", inv)
			+ ",\"pointX\":" + hit.point.x.ToString("0.###", inv) + ",\"pointY\":" + hit.point.y.ToString("0.###", inv) + damaged + "}";
	}
	return "{\"ok\":true"
		+ ",\"bodyX\":" + at.x.ToString("0.###", inv)
		+ ",\"bodyY\":" + at.y.ToString("0.###", inv)
		+ ",\"lookX\":" + look.x.ToString("0.###", inv)
		+ ",\"lookY\":" + look.y.ToString("0.###", inv)
		+ ",\"dirX\":" + dir.x.ToString("0.###", inv)
		+ ",\"dirY\":" + dir.y.ToString("0.###", inv)
		+ ",\"fromX\":" + from.x.ToString("0.###", inv)
		+ ",\"fromY\":" + from.y.ToString("0.###", inv)
		+ ",\"count\":" + count.ToString(inv)
		+ ",\"hits\":[" + text + "]}";
}))()
