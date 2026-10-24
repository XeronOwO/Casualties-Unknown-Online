// recipe: open-own-wound
// args: none
// serves: host-heartbeat-sound-truncated (the LOCAL panel's critical beep, the instance the user hears)
// returns: ok, open, error, detail
//
// Opens the local body's own medical panel through the game's own entry (`PlayerCamera.WoundViewButton`, the call the
// game's hotkey and the CUO remote view both use), so the panel the user is describing can be sampled where it runs.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	camera.WoundViewButton();
	var view = WoundView.view;
	return "{\"ok\":true,\"open\":" + (view != null && view.gameObject.activeInHierarchy ? "true" : "false") + "}";
}))()
