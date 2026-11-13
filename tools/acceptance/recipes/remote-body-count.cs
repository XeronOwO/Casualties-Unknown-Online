// recipe: remote-body-count
// args: mode=s
// serves: 1.0-regression-sweep (the removal case)
// returns: ok, mode, bodyTotal, remoteBodies, remoteNames, error
//
// Counts the player bodies in the scene that are not the camera's own. The displayed body of a
// remote player is an instance of the game's own body type, and the clone marker `remote-clone-probe`
// looks for (`RemoteCloneRender`) turned out to sit on cloned items, so this probe counts by type
// instead. The type comes from the local body instance because the recipe compiler does not resolve
// `Body` by name.
//
// Read-only.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var local = camera.body;
	var bodyType = ((object)local).GetType();
	var bodies = UnityEngine.Object.FindObjectsOfType(bodyType);
	var remote = 0;
	var names = "";
	for (var i = 0; i < bodies.Length; i++)
	{
		var candidate = bodies[i];
		if (candidate == null) { continue; }
		if (object.ReferenceEquals(candidate, (UnityEngine.Object)local)) { continue; }
		remote++;
		if (names.Length > 0) { names += ";"; }
		names += ((UnityEngine.Component)candidate).transform.root.name;
	}

	return "{\"ok\":true,\"mode\":" + "\"" + mode + "\""
		+ ",\"bodyTotal\":" + bodies.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"remoteBodies\":" + remote.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"remoteNames\":\"" + names + "\"}";
}))()
