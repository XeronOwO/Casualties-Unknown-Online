// recipe: attack-swing-probe
// args: x=n y=n
// returns: ok, conscious, cooldown, bodyX, bodyY, handX, handY, lookX, lookY, distance, aimDegrees, targetDistance,
//          inRange, hitName, hitPointX, hitPointY, damage
//
// Answers why a held attack key produces no damage on a cell: `Body.Attack` needs `conscious` and a spent cooldown,
// then rays from `limbs[1]` along `(targetLookPos - limbs[1].position)` for `atk.distance` and only damages what the
// ray hits (`Body.cs:2036` onward). The row reads this once before it swings, so a miss is named instead of guessed.
((System.Func<string>)(() => {
	var x = {{n:x}};
	var y = {{n:y}};
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var body = camera.body;
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var type = body.GetType();
	var cooldownField = type.GetField("attackCooldown", flags);
	var lookField = type.GetField("targetLookPos", flags);
	var cooldown = cooldownField == null ? -1f : System.Convert.ToSingle(cooldownField.GetValue(body));
	// `targetLookPos` is a Vector2 in the shipped build; read it without a hard cast so a component-shaped value does
	// not fail the whole probe with "Specified cast is not valid" (measured 2026-11-22: the first version threw that).
	var look = default(Vector2);
	var rawLook = lookField == null ? null : lookField.GetValue(body);
	if (rawLook is Vector2 flat) { look = flat; }
	else if (rawLook is Vector3 deep) { look = new Vector2(deep.x, deep.y); }
	else if (rawLook is Vector2Int grid) { look = new Vector2(grid.x, grid.y); }
	var hand = body.limbs[1].transform.position;
	var vector = (look - new Vector2(hand.x, hand.y));
	var distance = vector.magnitude;
	var aim = vector.normalized;
	var cellCentre = new Vector2(x + 0.5f, y + 0.5f);
	var toCell = cellCentre - new Vector2(hand.x, hand.y);
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	// The swing's own walk: `Body.cs:2168` takes `Physics2D.RaycastAll` from `limbs[1]` along the aim, skips the
	// player's own transform, and damages the first hit tagged `BlockGround`. A single `Physics2D.Raycast` stops on the
	// player's own collider and says the swing missed when the game would have gone on to the block (measured
	// 2026-11-22: the probe reported `hitName:"Body"` while the aim was already inside the cell).
	var firstOther = "";
	var blockHit = false;
	var blockPoint = Vector2.zero;
	var all = Physics2D.RaycastAll(new Vector2(hand.x, hand.y), aim, 4.5f);
	for (var i = 0; i < all.Length; i++) {
		var hitTransform = all[i].transform;
		if (hitTransform == null || hitTransform == body.transform) { continue; }
		if (firstOther.Length == 0) { firstOther = hitTransform.gameObject.name; }
		if (hitTransform.CompareTag("BlockGround")) { blockHit = true; blockPoint = all[i].point; break; }
	}
	var hitName = firstOther;
	var hit = default(RaycastHit2D);
	hit.point = blockHit ? blockPoint : Vector2.zero;
	var row = WorldGeneration.world.GetBlockDamage(new Vector2Int(x, y));
	var damage = row != null ? row.damage : -1f;
	return "{\"ok\":true,\"conscious\":" + (body.conscious ? "true" : "false")
		+ ",\"cooldown\":" + cooldown.ToString("0.###", inv)
		+ ",\"bodyX\":" + System.Convert.ToInt32(body.transform.position.x) + ",\"bodyY\":" + System.Convert.ToInt32(body.transform.position.y)
		+ ",\"handX\":" + hand.x.ToString("0.##", inv) + ",\"handY\":" + hand.y.ToString("0.##", inv)
		+ ",\"lookX\":" + look.x.ToString("0.##", inv) + ",\"lookY\":" + look.y.ToString("0.##", inv)
		+ ",\"distance\":" + distance.ToString("0.##", inv)
		+ ",\"aimDegrees\":" + (System.Math.Atan2(aim.y, aim.x) * 57.29578f).ToString("0.#", inv)
		+ ",\"targetDistance\":" + toCell.magnitude.ToString("0.##", inv)
		+ ",\"inRange\":" + (toCell.magnitude <= 4.5f ? "true" : "false")
		+ ",\"blockHit\":" + (blockHit ? "true" : "false")
		+ ",\"hitName\":\"" + hitName.Replace("\"", "") + "\""
		+ ",\"hitPointX\":" + hit.point.x.ToString("0.##", inv) + ",\"hitPointY\":" + hit.point.y.ToString("0.##", inv)
		+ ",\"damage\":" + damage.ToString("0.###", inv) + "}";
}))()
