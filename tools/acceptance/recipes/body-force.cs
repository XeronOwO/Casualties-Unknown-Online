// recipe: body-force
// args: consciousness=n energy=n brainHealth=n badSleepAmount=n idleTime=n intelligence=n heartRate=n bloodPressure=n bloodOxygen=n
// serves: carried-unconscious-body-simulation, carried-rider-own-body-stops-simulating,
//         host-severe-sleepiness-posture-desync, host-fall-injury-mouth-expression-desync,
//         carried-player-idle-sit-suppression, carrier-sit-when-carrying,
//         mod-item-declaration-parity (the recognition gate an item asks for is `min <= skills.INT`,
//         so a row about it puts the reading body below the gate)
// returns: ok, consciousness, energy, brainHealth, badSleepAmount, idleTime, intelligence, heartRate, bloodPressure, legSpeedMult, alive, conscious
//
// Forces the local body's own simulation inputs — the fields the game itself reads every frame — so a
// run can reach a state that needs no person at the keyboard. -1 leaves a field alone; the scenarios:
//   unconscious rider: brainHealth=1 consciousness=0                  (alive, consciousness cap ~1, stays down)
//   dead body:         brainHealth=0                                  (alive=false; classic carry still accepts it)
//   severe sleepiness: energy=0 badSleepAmount=150 consciousness=31   (legSpeedMult falls, body slouches)
//   idle sit:          idleTime=13                                    (the native sit condition fires next Update)
//   unrecognizing:     intelligence=0                                 (the game's recognition gate asks `min <= INT`,
//                                                                      which the game's own mindwipe reaches the
//                                                                      same way: INT=0, then UpdateExpBoundaries)
//   cardiac arrest:    heartRate=0 bloodPressure=5                    (Body.inCardiacArrest is heartRate < 20 — the state
//                                                                      CPR's start gate asks for; bloodPressure is what a
//                                                                      compression lifts, so a CPR row reads it before
//                                                                      and after)
//
// The field names are the game's own (Body.cs): consciousness, energy, brainHealth, badSleepAmount,
// idleTime; alive/conscious are their derived properties (brainHealth > 0, alive && consciousness > 30).
// `intelligence` is `Body.skills.INT`, the skill `Recognition.recognizable` compares against.
((System.Func<string>)(() => {
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	var consciousness = {{n:consciousness}};
	var energy = {{n:energy}};
	var brainHealth = {{n:brainHealth}};
	var badSleepAmount = {{n:badSleepAmount}};
	var idleTime = {{n:idleTime}};
	var intelligence = {{n:intelligence}};
	var heartRate = {{n:heartRate}};
	var bloodPressure = {{n:bloodPressure}};
	var bloodOxygen = {{n:bloodOxygen}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	if (consciousness >= 0f) { body.consciousness = consciousness; }
	if (energy >= 0f) { body.energy = energy; }
	if (brainHealth >= 0f) { body.brainHealth = brainHealth; }
	if (badSleepAmount >= 0f) { body.badSleepAmount = badSleepAmount; }
	if (idleTime >= 0f) { body.idleTime = idleTime; }
	if (intelligence >= 0f) { body.skills.INT = (int)intelligence; body.skills.UpdateExpBoundaries(); }
	if (heartRate >= 0f) { body.heartRate = heartRate; }
	if (bloodPressure >= 0f) { body.bloodPressure = bloodPressure; }
	if (bloodOxygen >= 0f) { body.bloodOxygen = bloodOxygen; }
	return "{\"ok\":true,\"consciousness\":" + num(body.consciousness)
		+ ",\"energy\":" + num(body.energy)
		+ ",\"brainHealth\":" + num(body.brainHealth)
		+ ",\"badSleepAmount\":" + num(body.badSleepAmount)
		+ ",\"idleTime\":" + num(body.idleTime)
		+ ",\"intelligence\":" + body.skills.INT.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"heartRate\":" + num(body.heartRate)
		+ ",\"bloodPressure\":" + num(body.bloodPressure)
		+ ",\"bloodOxygen\":" + num(body.bloodOxygen)
		+ ",\"legSpeedMult\":" + num(body.legSpeedMult)
		+ ",\"alive\":" + (body.alive ? "true" : "false")
		+ ",\"conscious\":" + (body.conscious ? "true" : "false") + "}";
}))()
