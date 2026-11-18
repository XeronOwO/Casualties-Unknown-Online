// recipe: capture-frame
// args: name=s
// serves: every visual row (the localization residuals, the clipped label, the drop sprites, the medical panel's look)
// returns: ok, path, width, height, error, detail
//
// Captures the CLIENT's own frame through `UnityEngine.ScreenCapture.CaptureScreenshot`, which writes the window's rendered
// pixels (the game view, not the desktop). The directory comes from CUO_FRAME_DIR when the batch sets it, otherwise the system
// temp directory, so no machine path is ever baked into a tracked file; a Sandboxie client writes inside its box and the batch
// copies the file out (the box's own root is a local, gitignored fact).
((System.Func<string>)(() => {
	var name = {{s:name}};
	var safe = name.Replace("/", "-").Replace("\\", "-");
	if (safe.Length == 0) { safe = "frame"; }
	var configured = System.Environment.GetEnvironmentVariable("CUO_FRAME_DIR");
	var directory = string.IsNullOrEmpty(configured)
		? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cuo-frames")
		: configured;
	System.IO.Directory.CreateDirectory(directory);
	var path = System.IO.Path.Combine(directory, safe + ".png");
	if (System.IO.File.Exists(path)) { System.IO.File.Delete(path); }
	UnityEngine.ScreenCapture.CaptureScreenshot(path);
	return "{\"ok\":true,\"path\":\"" + path.Replace("\\", "/") + "\""
		+ ",\"width\":" + UnityEngine.Screen.width.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"height\":" + UnityEngine.Screen.height.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
