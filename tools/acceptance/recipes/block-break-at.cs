// recipe: block-break-at
// args: x=n y=n dmg=n
// serves: guest-block-mutation-re-report rows 1, 3, 4 and 10, the swallowed mine/air-write family
// returns: ok, cellX, cellY, blockBefore, blockAfter, damage, error, detail
//
// Rolls the game's own WorldGeneration.DamageBlock at one ABSOLUTE cell — the choke point every native
// mining/attack roll enters and the path CUO's block-damage report is anchored on. A damage at or above
// the block's health breaks it, so the call reports and presents as a mine; the relative block-hit
// recipe cannot address a fixed cell once the three bodies drift apart.
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var cell = new Vector2Int((int)({{n:x}}), (int)({{n:y}}));
	var dmg = (float)({{n:dmg}});
	var before = world.GetBlock(cell);
	world.DamageBlock(cell, dmg, true, false, false);
	var row = world.GetBlockDamage(cell);
	var damage = row != null ? row.damage : -1f;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	return "{\"ok\":true"
		+ ",\"cellX\":" + cell.x.ToString(inv)
		+ ",\"cellY\":" + cell.y.ToString(inv)
		+ ",\"blockBefore\":" + ((int)before).ToString(inv)
		+ ",\"blockAfter\":" + ((int)world.GetBlock(cell)).ToString(inv)
		+ ",\"damage\":" + damage.ToString("0.###", inv) + "}";
}))()
