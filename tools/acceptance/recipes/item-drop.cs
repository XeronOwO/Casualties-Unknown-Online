// recipe: item-drop
// args: type=s height=n
// serves: suppressed-native-call-sounds-stay-unheard
// returns: ok, type, x, y, error, detail
//
// Spawns a world item `height` world units above the local body through the game's own `Utils.Create(string, Vector2, float)`
// (reversing/Assembly-CSharp/Assembly-CSharp/Utils.cs:10), so the item falls under the game's own physics and lands; the landing is the native
// collision presentation the item-impact carrier reports. `Vector2` because that is the overload the game itself uses for world items.
//
// ONE LINE ON PURPOSE: the client's evaluator is a Mono.CSharp REPL that takes one top-level expression, and a body that opens a block on one line and
// closes it on another is refused as `(1,1): InteractiveHost` (docs/backlog/done/new-recipe-files-are-rejected-by-the-evaluator.md).
((System.Func<string>)(() => { var type = {{s:type}}; var height = {{n:height}}; var body = PlayerCamera.main == null ? null : PlayerCamera.main.body; if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; } var origin = body.transform.position; var pos = new Vector2(origin.x, origin.y + height); var spawned = Utils.Create(type, pos, 0f); if (spawned == null) { return "{\"ok\":false,\"error\":\"create-failed\",\"detail\":\"Utils.Create returned nothing for " + type + "\"}"; } return "{\"ok\":true,\"type\":\"" + type + "\",\"x\":" + pos.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ",\"y\":" + pos.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "}"; }))()
