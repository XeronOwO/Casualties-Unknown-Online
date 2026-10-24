// recipe: screen-point2
// args: x=n y=n
// serves: cross-player-drag-use-model-bounds (the release point of a real drag, in the dragging client's pixels)
// returns: ok, worldX, worldY, screenX, screenY, height, error, detail
//
// Projects one world point through THIS client's own camera, in client pixels. Written the way the recipes the evaluator
// accepts are written: the placeholder cast to float before use (a bare numeric placeholder arrives as a double and fails the
// compile unit), `Vector3` unqualified, and every Unity type spelled out.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = UnityEngine.Camera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var worldX = (float)({{n:x}});
	var worldY = (float)({{n:y}});
	var point = new Vector3(worldX, worldY, 0f);
	var screen = camera.WorldToScreenPoint(point);
	return "{\"ok\":true,\"worldX\":" + worldX.ToString("0.###", inv)
		+ ",\"worldY\":" + worldY.ToString("0.###", inv)
		+ ",\"screenX\":" + ((int)screen.x).ToString(inv)
		+ ",\"screenY\":" + ((int)screen.y).ToString(inv)
		+ ",\"height\":" + UnityEngine.Screen.height.ToString(inv) + "}";
}))()
