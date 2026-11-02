// recipe: hover-probe
// args: layer=s
// returns: ok, mouseX, mouseY, worldX, worldY, hits, error, detail
//
// Answers what the game's own world hover WOULD see at the staged pointer: it reads `Input.mousePosition` as the game reads it,
// converts it with the game's own camera (`Camera.main.ScreenToWorldPoint`) and overlaps the layer the hover uses. It exists because
// the drag-use cue needs the teammate FOCUSED (`focusedSteamId`), that focus comes from this hover, and a staged pointer that the
// EventSystem ignores can still be checked here - separating "the pointer is in the wrong place" from "the pointer never reaches the
// hover".
((System.Func<string>)(() => {
	var camera = Camera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var mouse = UnityEngine.Input.mousePosition;
	var world = camera.ScreenToWorldPoint(mouse);
	var layer = {{s:layer}};
	var mask = layer.Length > 0 ? LayerMask.GetMask(new string[] { layer }) : Physics2D.DefaultRaycastLayers;
	var hit = Physics2D.OverlapPoint(new Vector2(world.x, world.y), mask);
	var names = "";
	var count = 0;
	var all = Physics2D.OverlapPointAll(new Vector2(world.x, world.y), mask);
	if (all != null)
	{
		count = all.Length;
		for (var i = 0; i < all.Length && i < 6; i++)
		{
			names = names + all[i].gameObject.name + "(" + all[i].gameObject.tag + ")|";
		}
	}

	return "{\"ok\":true,\"mouseX\":" + mouse.x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"mouseY\":" + mouse.y.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"worldX\":" + world.x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"worldY\":" + world.y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"count\":" + count
		+ ",\"hits\":\"" + names + "\"}";
}))()
