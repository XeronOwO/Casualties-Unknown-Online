// recipe: body-read
// args: none
// serves: carried-player-idle-sit-suppression, carrier-sit-while-carrying, carried-unconscious-body-simulation,
//         host-severe-sleepiness-posture-desync, host-fall-injury-mouth-expression-desync
// returns: ok, local, role, inWorld, localCarries, localCarriedBy, localDriverCarrier, localBody
//
// The local body's own simulation state, as one client sees it: the pose flags, the vitals inputs, the
// move intent and the computed leg-speed multiplier, plus the carry relation mirror and the carried-body
// driver on the body. A run reads it on every client to compare what each side simulates.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	var findType = new System.Func<string, System.Type>((fullName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type found = null;
			try { found = assemblies[i].GetType(fullName, false); } catch { found = null; }
			if (found != null) { return found; }
		}
		return null;
	});
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService;
	var control = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl)) as CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl;
	if (session == null || control == null) { return "{\"ok\":false,\"error\":\"no-player-interaction\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var localDriverCarrier = 0UL;
	var carriedDriverType = findType("CasualtiesUnknownOnline.GameAdapter.Character.CarriedBodyDriver");
	if (carriedDriverType != null) {
		var localDriver = body.GetComponent(carriedDriverType);
		if (localDriver != null) {
			var carrierField = carriedDriverType.GetField("CarrierSteamId", instanceFlags);
			if (carrierField != null) { localDriverCarrier = System.Convert.ToUInt64(carrierField.GetValue(localDriver)); }
		}
	}
	var localCarried = 0UL;
	var localCarrier = 0UL;
	var carries = control.TryGetCarried(session.LocalSteamId, out localCarried);
	var carriedBy = control.TryGetCarrier(session.LocalSteamId, out localCarrier);
	var clip = "";
	var clips = body.bodyAnimator.GetCurrentAnimatorClipInfo(0);
	if (clips != null && clips.Length > 0) { clip = clips[0].clip.name; }
	return "{\"ok\":true"
		+ ",\"local\":\"" + session.LocalSteamId + "\""
		+ ",\"role\":\"" + session.Role + "\""
		+ ",\"inWorld\":" + (session.LocalInWorld ? "true" : "false")
		+ ",\"localCarries\":\"" + (carries ? localCarried.ToString() : "0") + "\""
		+ ",\"localCarriedBy\":\"" + (carriedBy ? localCarrier.ToString() : "0") + "\""
		+ ",\"localDriverCarrier\":\"" + localDriverCarrier.ToString() + "\""
		+ ",\"localBody\":{"
		+ "\"standing\":" + (body.standing ? "true" : "false")
		+ ",\"sleeping\":" + (body.sleeping ? "true" : "false")
		+ ",\"crouching\":" + (body.crouching ? "true" : "false")
		+ ",\"alive\":" + (body.alive ? "true" : "false")
		+ ",\"conscious\":" + (body.conscious ? "true" : "false")
		+ ",\"consciousness\":" + num(body.consciousness)
		+ ",\"brainHealth\":" + num(body.brainHealth)
		+ ",\"energy\":" + num(body.energy)
		+ ",\"badSleepAmount\":" + num(body.badSleepAmount)
		+ ",\"idleTime\":" + num(body.idleTime)
		+ ",\"moveDirX\":" + num(body.moveDir.x)
		+ ",\"moveDirY\":" + num(body.moveDir.y)
		+ ",\"velocityX\":" + num(body.rb.velocity.x)
		+ ",\"velocityY\":" + num(body.rb.velocity.y)
		+ ",\"legSpeedMult\":" + num(body.legSpeedMult)
		+ ",\"positionX\":" + num(body.transform.position.x)
		+ ",\"positionY\":" + num(body.transform.position.y)
		+ ",\"animatorClip\":\"" + clip + "\"}}";
}))()
