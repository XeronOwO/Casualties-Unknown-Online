// recipe: pointer-target
// args: x=n y=n
// returns: ok, cellX, cellY, worldX, worldY, screenX, screenY, inputX, inputY, pointerWorldX, pointerWorldY,
//          backCellX, backCellY, targetDistance, error, detail
//
// Answers one question for a row that aims at a block: does the position it staged come back as that block?
// It takes the block's own world point (`BlockToWorldPos`), projects it through the game's camera, reads the pointer the
// game actually sees (`Input.mousePosition`), maps that pointer back into the world (`ScreenToWorldPoint`) and finally
// back into a block (`WorldToBlockPos`). When `backCellX/backCellY` are not the cell the row asked for, the pointer never
// reached the block and no swing can land there however long the attack is held (measured 2026-11-22: the row staged
// (662, 384), the game read (1728, -159), and a cell four blocks away projected to y = -7608).
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var world = WorldGeneration.world;
	var cam = Camera.main;
	if (world == null || cam == null) { return "{\"ok\":false,\"error\":\"no-world-or-camera\"}"; }
	var cell = new Vector2Int((int)({{n:x}}), (int)({{n:y}}));
	Vector3 at;
	try { at = world.BlockToWorldPos(cell); }
	catch (Exception error) { return "{\"ok\":false,\"error\":\"block-to-world\",\"detail\":\"" + error.GetType().Name + ": " + error.Message.Replace("\"", "''") + "\"}"; }
	var screen = cam.WorldToScreenPoint(at);
	var input = Input.mousePosition;
	var pointerWorld = cam.ScreenToWorldPoint(input);
	Vector2Int back;
	try { back = world.WorldToBlockPos(pointerWorld); }
	catch (Exception error) { back = new Vector2Int(int.MinValue, int.MinValue); }
	var target = new Vector3((float)cell.x, (float)cell.y, 0f);
	return "{\"ok\":true"
		+ ",\"cellX\":" + cell.x.ToString(inv)
		+ ",\"cellY\":" + cell.y.ToString(inv)
		+ ",\"worldX\":" + at.x.ToString("0.####", inv)
		+ ",\"worldY\":" + at.y.ToString("0.####", inv)
		+ ",\"screenX\":" + screen.x.ToString("0.#", inv)
		+ ",\"screenY\":" + screen.y.ToString("0.#", inv)
		+ ",\"inputX\":" + input.x.ToString("0.#", inv)
		+ ",\"inputY\":" + input.y.ToString("0.#", inv)
		+ ",\"pointerWorldX\":" + pointerWorld.x.ToString("0.####", inv)
		+ ",\"pointerWorldY\":" + pointerWorld.y.ToString("0.####", inv)
		+ ",\"backCellX\":" + back.x.ToString(inv)
		+ ",\"backCellY\":" + back.y.ToString(inv)
		+ ",\"targetDistance\":" + Vector3.Distance(pointerWorld, at).ToString("0.####", inv)
		+ "}";
}))()
