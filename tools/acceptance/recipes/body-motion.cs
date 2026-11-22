// recipe: body-motion
// args: none
// returns: ok, moveX, moveY, posX, posY, velX, velY, instanceId, bodyName
//
// Reads what the local player's body is actually doing: the movement vector the game set from its own binds
// (`PlayerCamera.cs:1066-1067` writes `body.moveDir` from `Input.GetKey(...)`) and the body's velocity and position.
// A row that stages a movement key and sees the body stay put can tell whether the game never read the key or the body
// is being held back (measured 2026-11-22: twenty-four 400 ms steps moved the body one unit).
//
// `instanceId` and `bodyName` are here because two samples of the same walk read x = +4.085 and x = -4.085, and a
// position that mirrors between samples means the row is not reading the same body twice.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => { var body = PlayerCamera.main == null ? null : PlayerCamera.main.body; if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; } var dir = body.moveDir; var unit = body.GetComponent<Rigidbody2D>(); var at = body.transform.position; var vx = unit == null ? 0f : unit.velocity.x; var vy = unit == null ? 0f : unit.velocity.y; var name = body.gameObject == null ? "" : body.gameObject.name.Replace("\"", "''"); return "{\"ok\":true,\"moveX\":" + dir.x + ",\"moveY\":" + dir.y + ",\"posX\":" + at.x + ",\"posY\":" + at.y + ",\"velX\":" + vx + ",\"velY\":" + vy + ",\"instanceId\":" + body.GetInstanceID() + ",\"bodyName\":\"" + name + "\"}"; }))()

