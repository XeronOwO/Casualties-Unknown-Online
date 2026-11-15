// recipe: character-read
// args: none
// returns: ok, selected, charType, hasBody, wonchar0, wonchar1, error
//
// Reads the client's current character choice and the body the game actually spawned from it, plus
// the two solo unlock keys this ticket must never write. Run it on each client; the body is the
// player-visible fact, not the menu field.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	var body = camera == null ? null : camera.body;
	var selected = PreRunScript.characterSelected;
	var charType = body == null ? -1 : body.charType;
	return "{\"ok\":true,\"selected\":" + selected
		+ ",\"charType\":" + charType
		+ ",\"hasBody\":" + (body == null ? "false" : "true")
		+ ",\"wonchar0\":" + UnityEngine.PlayerPrefs.GetInt("wonchar0", 0)
		+ ",\"wonchar1\":" + UnityEngine.PlayerPrefs.GetInt("wonchar1", 0) + "}";
}))()
