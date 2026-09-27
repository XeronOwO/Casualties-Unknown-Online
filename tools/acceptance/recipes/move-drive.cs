// recipe: move-drive
// args: x=n y=n speed=n crouch=n facing=n mode=s
// serves: carry-piggyback-rider-position-smoothing, carry-piggyback-vertical-placement-asymmetry,
//         carrier-sit-while-carrying, carried-player-idle-sit-suppression, carried-unconscious-body-simulation,
//         carried-rider-own-body-stops-simulating
// returns: ok, mode, moveDirX, moveDirY, velocityX, velocityY, crouching, isRight, positionX, positionY
//
// One movement nudge for the local body, so a participant's carrier can be moved without a person at the
// keyboard. The game's own PlayerCamera rewrites the move intent from the keyboard every frame, so one
// call is one frame's worth of motion: the run calls it repeatedly for the observation window and
// confirms on the returned position that the body actually travelled.
//   mode=walk  — write only Body.moveDir (the game's own physics walks the body)
//   mode=slide — write the horizontal velocity too — Body.rb.velocity.x = x * speed, the vertical
//                component kept — for when the frame order eats the intent before the physics step
// x is the horizontal intent (-1 left .. 1 right) and y the game's vertical intent (up = +1, the value
// the game's own jump/climb path reads); slide never writes the vertical velocity, so one call cannot
// fling the body off the ground. crouch: -1 leave, 0 stand, 1 crouch; facing: -1 leave, 0 left,
// 1 right. The returned position lets the caller confirm the body actually travelled.
((System.Func<string>)(() => {
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	var x = {{n:x}};
	var y = {{n:y}};
	var speed = {{n:speed}};
	var crouch = {{n:crouch}};
	var facing = {{n:facing}};
	var mode = {{s:mode}};
	if (mode != "walk" && mode != "slide") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be walk or slide\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	body.moveDir = new UnityEngine.Vector2(x, y);
	if (crouch >= 0f) { body.crouching = crouch >= 0.5f; }
	if (facing >= 0f) { body.isRight = facing >= 0.5f; }
	if (mode == "slide" && body.rb != null) { body.rb.velocity = new UnityEngine.Vector2(x * speed, body.rb.velocity.y); }
	return "{\"ok\":true"
		+ ",\"mode\":\"" + mode + "\""
		+ ",\"moveDirX\":" + num(body.moveDir.x)
		+ ",\"moveDirY\":" + num(body.moveDir.y)
		+ ",\"velocityX\":" + num(body.rb.velocity.x)
		+ ",\"velocityY\":" + num(body.rb.velocity.y)
		+ ",\"crouching\":" + (body.crouching ? "true" : "false")
		+ ",\"isRight\":" + (body.isRight ? "true" : "false")
		+ ",\"positionX\":" + num(body.transform.position.x)
		+ ",\"positionY\":" + num(body.transform.position.y) + "}";
}))()
