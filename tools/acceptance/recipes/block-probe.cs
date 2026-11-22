// recipe: block-probe
// args: x=n y=n
// returns: ok, cellX, cellY, worldX, worldY, hits, tag, layer, layerName, blockGround, detail
//
// Answers whether the cell a row picked is something a swing can damage: it asks the physics scene what sits at the
// block's centre and names its tag and layer. The game damages only colliders tagged `BlockGround` (Body.cs:2162-2168),
// so a cell that reads a block number in the world grid but carries another tag (a wall, a background tile, a prop) never
// takes damage however long the attack is held — the row needs to know that before it swings, not after a timeout.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var cellX = (int)({{n:x}});
	var cellY = (int)({{n:y}});
	var at = new Vector2(cellX + 0.5f, cellY + 0.5f);
	var found = Physics2D.OverlapPointAll(at);
	var tags = new System.Text.StringBuilder();
	var layers = new System.Text.StringBuilder();
	var blockGround = false;
	for (var i = 0; i < found.Length; i++)
	{
		var one = found[i];
		if (i > 0) { tags.Append("|"); layers.Append("|"); }
		tags.Append(one.gameObject.tag);
		layers.Append(LayerMask.LayerToName(one.gameObject.layer));
		if (one.gameObject.CompareTag("BlockGround")) { blockGround = true; }
	}
	return "{\"ok\":true"
		+ ",\"cellX\":" + cellX.ToString(inv)
		+ ",\"cellY\":" + cellY.ToString(inv)
		+ ",\"worldX\":" + at.x.ToString("0.###", inv)
		+ ",\"worldY\":" + at.y.ToString("0.###", inv)
		+ ",\"hits\":" + found.Length.ToString(inv)
		+ ",\"tag\":\"" + tags.ToString() + "\""
		+ ",\"layer\":\"" + layers.ToString() + "\""
		+ ",\"blockGround\":" + (blockGround ? "true" : "false")
		+ "}";
}))()
