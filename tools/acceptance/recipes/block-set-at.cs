// recipe: block-set-at
// args: x=n y=n block=n
// serves: guest-block-mutation-re-report rows 2 and 10, the swallowed block-placement report
// returns: ok, cellX, cellY, blockBefore, blockAfter, error, detail
//
// Writes one ABSOLUTE cell through the game's own WorldGeneration.SetBlock, the single post-generation
// write entry every native placement path lands on (CUO's own report hook is its Harmony postfix, so a
// call here reports exactly as a native placement does; 1 = light rock, 2 = gravel, 0 = air).
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var cell = new Vector2Int((int)({{n:x}}), (int)({{n:y}}));
	var block = (ushort)({{n:block}});
	var before = world.GetBlock(cell);
	world.SetBlock(cell, block);
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	return "{\"ok\":true"
		+ ",\"cellX\":" + cell.x.ToString(inv)
		+ ",\"cellY\":" + cell.y.ToString(inv)
		+ ",\"blockBefore\":" + ((int)before).ToString(inv)
		+ ",\"blockAfter\":" + ((int)world.GetBlock(cell)).ToString(inv) + "}";
}))()
