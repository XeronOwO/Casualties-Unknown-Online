// recipe: moodle-body
// args: none
// serves: spectator-presentation (the HUD manager's body must return to the local body when the watch ends)
// returns: ok, managerBody, managerBodyAlive, cameraBody, same, error
//
// Reads the private MoodleManager.body field the spectator HUD patch repoints. While watching it must name the
// watched body; after the mode ends (and the clone is destroyed) it must name the local camera body, or the native
// UpdateMoodles path calls AddAllMoodles on a destroyed object every frame.
((System.Func<string>)(() => {
	var manager = UnityEngine.Object.FindObjectOfType<MoodleManager>();
	if (manager == null) { return "{\"ok\":false,\"error\":\"no-manager\"}"; }
	var camera = PlayerCamera.main;
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var field = typeof(MoodleManager).GetField("body", flags);
	if (field == null) { return "{\"ok\":false,\"error\":\"no-body-field\"}"; }
	var body = field.GetValue(manager) as Body;
	var local = camera != null ? camera.body : null;
	var bodyName = body != null ? body.name : "<destroyed>";
	var localName = local != null ? local.name : "<none>";
	return "{\"ok\":true"
		+ ",\"managerBody\":\"" + bodyName + "\""
		+ ",\"managerBodyAlive\":" + (body != null ? "true" : "false")
		+ ",\"cameraBody\":\"" + localName + "\""
		+ ",\"same\":" + (body != null && local != null && body == local ? "true" : "false") + "}";
}))()
