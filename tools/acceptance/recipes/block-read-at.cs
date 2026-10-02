// recipe: block-read-at
// args: x=n y=n
// serves: guest-block-mutation-re-report rows 1, 2, 3, 4 and 10, the world-block divergence family
// returns: ok, cellX, cellY, block, damage, error, detail
//
// Reads one ABSOLUTE cell of the live layer, so three clients whose bodies drift independently can be
// compared cell for cell (the relative block-read recipe names a different cell on every client).
// `damage` is the game's accumulated damage on the cell, -1 when the cell carries no row.
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var cell = new Vector2Int((int)({{n:x}}), (int)({{n:y}}));
	var row = world.GetBlockDamage(cell);
	var damage = row != null ? row.damage : -1f;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	return "{\"ok\":true"
		+ ",\"cellX\":" + cell.x.ToString(inv)
		+ ",\"cellY\":" + cell.y.ToString(inv)
		+ ",\"block\":" + ((int)world.GetBlock(cell)).ToString(inv)
		+ ",\"damage\":" + damage.ToString("0.###", inv) + "}";
}))()
