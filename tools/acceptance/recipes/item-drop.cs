// recipe: item-drop
// args: type=s height=n
// serves: suppressed-native-call-sounds-stay-unheard
// returns: ok, created, height
//
// Drops one world item through the game's own factory (`Utils.Create(string, Vector2, float)`, reversing/Assembly-CSharp/Assembly-CSharp/Utils.cs:10) at
// `height` world units above the local body, so the fall and the landing are the game's own physics and the game's own landing sound. `Vector2` because
// that is the overload the game uses for world items.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression, and a body that opens a block on one line and closes it on another is
// refused as `(1,1): InteractiveHost` (docs/backlog/done/new-recipe-files-are-rejected-by-the-evaluator.md).
((System.Func<string>)(() => { var kind = {{s:type}}; var lift = (float)({{n:height}}); var body = PlayerCamera.main == null ? null : PlayerCamera.main.body; if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; } var p = new Vector2(body.transform.position.x, body.transform.position.y + lift); var made = Utils.Create(kind, p, 0f); return "{\"ok\":true,\"created\":" + (made == null ? "false" : "true") + ",\"height\":" + lift.ToString() + "}"; }))()