// recipe: drag-state
// args: none
// serves: cross-player-drag-use-model-bounds (whether an OS press engages the game's own drag bracket)
// returns: ok, radialOpen, dragging, dragItem, dragType, mouseX, mouseY, error, detail
//
// Reads the game's own drag bracket: `PlayerCamera.dragItem` is what `HandleStartDragging` sets and `HandleReleaseDragging`
// consumes, so a non-null value while a press is held is the proof that the OS gesture engaged the native path (and a null
// value while the pointer is clearly pressed is the proof it did not). `raycastTargets` is not read: it changes every frame.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var field = typeof(PlayerCamera).GetField("dragItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	var drag = field == null ? null : field.GetValue(camera) as Item;
	var mouse = UnityEngine.Input.mousePosition;
	return "{\"ok\":true"
		+ ",\"radialOpen\":" + (camera.radialOpen ? "true" : "false")
		+ ",\"dragging\":" + (drag != null ? "true" : "false")
		+ ",\"dragItem\":\"" + (drag == null ? "none" : drag.id) + "\""
		+ ",\"dragType\":\"" + (drag == null ? "none" : drag.GetType().Name) + "\""
		+ ",\"mouseX\":" + ((int)mouse.x).ToString(inv)
		+ ",\"mouseY\":" + ((int)mouse.y).ToString(inv) + "}";
}))()
