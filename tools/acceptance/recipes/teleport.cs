// recipe: teleport
// args: x=n y=n
// returns: ok, fromX, fromY, toX, toY, moved, error, detail
//
// Puts the LOCAL body at (x, y) so a batch can separate two players that spawned on the same tile — the shape the off-screen arrow rows need, and the one the walk nudge
// cannot reach when the terrain boxes the body in (measured 2026-10-31: both bodies stopped at the same wall after two 12-unit steps). It writes the body's own
// transform, which is what the game's own position reads (and what the render clone's head anchor follows); the game's physics owns the body afterwards, so a caller
// reads the returned position to learn whether the body stayed. Diagnostic tool only: no shipping feature teleports a player.
((System.Func<string>)(() => {
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var t = body.transform;
	var fromX = t.position.x;
	var fromY = t.position.y;
	t.position = new UnityEngine.Vector3(x, y, t.position.z);
	var moved = (fromX != x) || (fromY != y);
	return "{\"ok\":true,\"fromX\":" + fromX.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"fromY\":" + fromY.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"toX\":" + t.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"toY\":" + t.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"moved\":" + (moved ? "true" : "false") + "}";
}))()
