// recipe: customize-close
// args: none
// serves: 1.0-rebuild-each-acceptance-row-from-the-game (row 9, item-customization)
// returns: ok, active, error
//
// Closes the game's customizer the way the escape key does (`PlayerCamera.cs:1004-1006`), so the row can read the
// other client after the player is done with the screen.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	camera.CloseItemCustomizer();
	var active = camera.itemCustomizer != null && camera.itemCustomizer.activeSelf;
	return "{\"ok\":true,\"active\":" + (active ? "true" : "false") + "}";
}))()
