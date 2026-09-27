// recipe: body-force
// args: consciousness=n energy=n brainHealth=n badSleepAmount=n idleTime=n
// serves: carried-unconscious-body-simulation, carried-rider-own-body-stops-simulating,
//         host-severe-sleepiness-posture-desync, host-fall-injury-mouth-expression-desync,
//         carried-player-idle-sit-suppression, carrier-sit-while-carrying
// returns: ok, consciousness, energy, brainHealth, badSleepAmount, idleTime, legSpeedMult, alive, conscious
//
// Forces the local body's own simulation inputs — the fields the game itself reads every frame — so a
// run can reach a state that needs no person at the keyboard. -1 leaves a field alone; the scenarios:
//   unconscious rider: brainHealth=1 consciousness=0                  (alive, consciousness cap ~1, stays down)
//   dead body:         brainHealth=0                                  (alive=false; classic carry still accepts it)
//   severe sleepiness: energy=0 badSleepAmount=150 consciousness=31   (legSpeedMult falls, body slouches)
//   idle sit:          idleTime=13                                    (the native sit condition fires next Update)
//
// The field names are the game's own (Body.cs): consciousness, energy, brainHealth, badSleepAmount,
// idleTime; alive/conscious are their derived properties (brainHealth > 0, alive && consciousness > 30).
((System.Func<string>)(() => {
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	var consciousness = {{n:consciousness}};
	var energy = {{n:energy}};
	var brainHealth = {{n:brainHealth}};
	var badSleepAmount = {{n:badSleepAmount}};
	var idleTime = {{n:idleTime}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	if (consciousness >= 0f) { body.consciousness = consciousness; }
	if (energy >= 0f) { body.energy = energy; }
	if (brainHealth >= 0f) { body.brainHealth = brainHealth; }
	if (badSleepAmount >= 0f) { body.badSleepAmount = badSleepAmount; }
	if (idleTime >= 0f) { body.idleTime = idleTime; }
	return "{\"ok\":true,\"consciousness\":" + num(body.consciousness)
		+ ",\"energy\":" + num(body.energy)
		+ ",\"brainHealth\":" + num(body.brainHealth)
		+ ",\"badSleepAmount\":" + num(body.badSleepAmount)
		+ ",\"idleTime\":" + num(body.idleTime)
		+ ",\"legSpeedMult\":" + num(body.legSpeedMult)
		+ ",\"alive\":" + (body.alive ? "true" : "false")
		+ ",\"conscious\":" + (body.conscious ? "true" : "false") + "}";
}))()
