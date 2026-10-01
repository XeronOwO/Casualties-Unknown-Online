// recipe: item-drop
// args: type=s height=n
// serves: suppressed-native-call-sounds-stay-unheard
// returns: ok, type, x, y, error, detail
//
// Spawns a world item `height` world units above the local body through the game's own Utils.Create, so
// the item falls under the game's own physics and lands; the landing is the native collision
// presentation the item-impact carrier reports. The run waits for the landing (bounded) before reading
// the logs, and drops a matching item on the other client for the guest-side half.
((System.Func<string>)(() => {
	var type = {{s:type}};
	var height = {{n:height}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var position = body.transform.position + new Vector3(0f, height, 0f);
	var spawned = Utils.Create(type, position, 0f);
	if (spawned == null) { return "{\"ok\":false,\"error\":\"create-failed\",\"detail\":\"Utils.Create returned nothing for " + type + "\"}"; }
	return "{\"ok\":true,\"type\":\"" + type + "\""
		+ ",\"x\":" + position.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"y\":" + position.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
