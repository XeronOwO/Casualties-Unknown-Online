// recipe: ui-casts
// args: none
// serves: cross-player-drag-use-model-bounds (which UI object the press lands on, and whether the native pick would take it)
// returns: ok, count, at, casts (name:layer:hasInvButton:hasItemLabel:overlaps), mouseX, mouseY, dragItem, error, detail
//
// Reads the very list the game's own `TryPickupFromUI` walks: `UIUtil.GetEventSystemRaycastResults()` for the current pointer,
// with each hit's GameObject name, layer, whether that object itself carries `InvButton` (the native pick requires the
// component on the hit object, not on a parent) and whether `InvButton.Overlaps` accepts the pointer. It also reports
// `PlayerCamera.dragItem`, so one call says whether the press picked an item and, if not, which of the two guards refused.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var casts = UIUtil.GetEventSystemRaycastResults();
	var parts = new System.Collections.Generic.List<string>();
	for (var i = 0; i < casts.Count; i++) {
		var hit = casts[i];
		var go = hit.gameObject;
		if (go == null) { continue; }
		var button = go.GetComponent<InvButton>();
		var label = go.GetComponent<ItemLabel>();
		var overlaps = button == null ? "n/a" : (button.Overlaps(casts) ? "true" : "false");
		parts.Add(go.name + ":" + go.layer.ToString(inv) + ":" + (button != null ? "inv" : "-") + ":" + (label != null ? "label" : "-") + ":" + overlaps);
	}
	var field = typeof(PlayerCamera).GetField("dragItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	var drag = field == null ? null : field.GetValue(camera) as Item;
	var mouse = UnityEngine.Input.mousePosition;
	// The ring's own geometry: `InvButton.Overlaps` accepts a centre-class button only inside a band measured from the ring's
	// centre divided by `PlayerCamera.uiScale` (152..281), so a press point that is not in that band can never pick anything.
	var ring = camera.radialMenu == null ? Vector2.zero : (Vector2)camera.radialMenu.position;
	var scale = PlayerCamera.uiScale <= 0f ? 1f : PlayerCamera.uiScale;
	var distanceOverScale = Vector2.Distance(ring, (Vector2)mouse) / scale;
	return "{\"ok\":true,\"count\":" + casts.Count.ToString(inv)
		+ ",\"ringX\":" + ((int)ring.x).ToString(inv)
		+ ",\"ringY\":" + ((int)ring.y).ToString(inv)
		+ ",\"uiScale\":" + scale.ToString("0.###", inv)
		+ ",\"distanceOverScale\":" + distanceOverScale.ToString("0.###", inv)
		+ ",\"at\":\"" + ((int)mouse.x).ToString(inv) + "," + ((int)mouse.y).ToString(inv) + "\""
		+ ",\"mouseX\":" + ((int)mouse.x).ToString(inv)
		+ ",\"mouseY\":" + ((int)mouse.y).ToString(inv)
		+ ",\"dragItem\":\"" + (drag == null ? "none" : drag.id) + "\""
		+ ",\"casts\":\"" + string.Join("|", parts) + "\"}";
}))()
