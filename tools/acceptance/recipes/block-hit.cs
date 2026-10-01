// recipe: block-hit
// args: dx=n dy=n dmg=n
// serves: guest-hears-only-some-block-break-sounds, unhooked-damage-block-callers
// returns: ok, cellX, cellY, blockBefore, blockAfter, error, detail
//
// Rolls the game's own WorldGeneration.DamageBlock — the choke point every native mining/attack roll
// enters, and the method CUO anchors its block-damage report on — at the cell dx/dy away from the
// local body's own cell. A damage below the block's health plays the block's hit sound; a damage at or
// above it breaks the block (hit sound, random step clip, dust, loot). One call per invocation: the
// run loops it for repeated hits, so the report cadence is the run's own step cadence.
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (world == null || body == null) { return "{\"ok\":false,\"error\":\"no-world-or-body\"}"; }
	var dx = {{n:dx}};
	var dy = {{n:dy}};
	var dmg = {{n:dmg}};
	var cell = world.WorldToBlockPos(body.transform.position);
	cell = new Vector2Int(cell.x + (int)dx, cell.y + (int)dy);
	var before = world.GetBlock(cell);
	world.DamageBlock(cell, dmg, true, false, false);
	var after = world.GetBlock(cell);
	return "{\"ok\":true,\"cellX\":" + cell.x.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"cellY\":" + cell.y.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"blockBefore\":" + ((int)before).ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"blockAfter\":" + ((int)after).ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
