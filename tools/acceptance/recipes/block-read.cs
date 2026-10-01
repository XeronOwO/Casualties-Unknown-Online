// recipe: block-read
// args: dx=n dy=n
// serves: guest-hears-only-some-block-break-sounds, unhooked-damage-block-callers
// returns: ok, cellX, cellY, block, damage, error, detail
//
// Reads the game's own block table and damage row at the cell dx/dy away from the local body's cell,
// so a run can compare what each client's world holds after a reported hit or break. `damage` is the
// accumulated damage on the cell, -1 when the cell carries no row.
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (world == null || body == null) { return "{\"ok\":false,\"error\":\"no-world-or-body\"}"; }
	var dx = {{n:dx}};
	var dy = {{n:dy}};
	var cell = world.WorldToBlockPos(body.transform.position);
	cell = new Vector2Int(cell.x + (int)dx, cell.y + (int)dy);
	var row = world.GetBlockDamage(cell);
	var damage = row != null ? row.damage : -1f;
	return "{\"ok\":true,\"cellX\":" + cell.x.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"cellY\":" + cell.y.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"block\":" + ((int)world.GetBlock(cell)).ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"damage\":" + damage.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
