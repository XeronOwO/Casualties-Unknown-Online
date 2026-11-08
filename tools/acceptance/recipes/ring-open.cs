// recipe: ring-open
// args: none
// serves: cross-player-drag-use-feedback, cross-player-drag-use-model-bounds
// Opens the player's inventory ring the way the game's own click handler does (it sets `radialOpen`), so a
// staged drag can start from the item's own button instead of a bracket armed in the middle. Read
// `ring-buttons` afterwards; a false answer means the body is not in a state the game lets open the ring.
((System.Func<string>)(() =>
{
	var camera = PlayerCamera.main;
	if (camera == null)
	{
		return "{\"ok\":false,\"error\":\"no camera\"}";
	}

	camera.radialOpen = true;
	return "{\"ok\":true,\"radialOpen\":" + (camera.radialOpen ? "true" : "false")
		+ ",\"dragItem\":" + (camera.dragItem == null ? "\"\"" : ("\"" + camera.dragItem.name + "\"")) + "}";
}))()
