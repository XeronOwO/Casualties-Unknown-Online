// recipe: character-read
// args: none
// returns: ok, selected, charType, hasBody, remoteCharTypes, wonchar0, wonchar1, error
//
// Reads the client's current character choice and the body the game actually spawned from it, plus
// the two solo unlock keys this ticket must never write. Run it on each client; the body is the
// player-visible fact, not the menu field.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	var body = camera == null ? null : camera.body;
	var selected = PreRunScript.characterSelected;
	var charType = body == null ? -1 : body.charType;
	System.Type driverType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && driverType == null; i++) {
		driverType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver", false);
	}
	var remoteCharTypes = "";
	if (driverType != null) {
		var bodies = UnityEngine.Object.FindObjectsOfType<Body>();
		for (var i = 0; i < bodies.Length; i++) {
			var candidate = bodies[i];
			if (candidate == null || candidate.GetComponent(driverType) == null) { continue; }
			if (remoteCharTypes.Length > 0) { remoteCharTypes += ","; }
			remoteCharTypes += candidate.charType.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
	}
	return "{\"ok\":true,\"selected\":" + selected
		+ ",\"charType\":" + charType
		+ ",\"hasBody\":" + (body == null ? "false" : "true")
		+ ",\"remoteCharTypes\":\"" + remoteCharTypes + "\""
		+ ",\"wonchar0\":" + UnityEngine.PlayerPrefs.GetInt("wonchar0", 0)
		+ ",\"wonchar1\":" + UnityEngine.PlayerPrefs.GetInt("wonchar1", 0) + "}";
}))()
