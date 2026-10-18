// recipe: health-read
// args: source=s target=s
// serves: heal-cheat-command-state-reset, health-simulation-and-host-authority
// returns: ok, source, alive, conscious, brainHealth, consciousness, bloodVolume, bloodOxygen, bloodPressure,
//          heartRate, respiratoryRate, bloodViscosity, fibrillationProgress, fibrillationForced, shock,
//          painShock, temperature, hunger, thirst, septicShock, sicknessAmount, stamina, energy, happiness,
//          weightOffset, radiationSickness, disfigured, eyeGone, bothEyesGone, internalBleeding, hemothorax,
//          traumaAmount, dirtyness, wetness, badSleepAmount, hearingLoss, adrenaline, curAdrenaline,
//          venomCurrent, venomTotal, clawHealth, caffeinated, opiateHappiness, antidepressantHappiness,
//          lastStandTime, triedRollingLastStand, succesfullyRolledLastStand, mindwipePresent,
//          mindwipeActive, sleepingPillsAmount, antidepressantsAmount, opiateAmount, error, detail
//
// The body-state read the `/heal` rows compare: the patient's own client reads its LIVE body
// (`source=local`), and the host reads the character snapshot it stores for a member
// (`source=saved`, `target=<member id>`) — the copy every peer's clone and the next save/reconnect
// are driven from. One read names the whole reset surface at once, so the row does not have to
// infer a field from a moodle or a pose. The medication components are read as the presence their
// policy maps (amount zero = removed), and the hollow as the component pair the moodle draws from.
// The two additive happiness terms (`opiateHappiness`/`antidepressantHappiness`) are body fields,
// not wire fields, so the saved branch cannot carry them: the local branch reads them.
((System.Func<string>)(() => {
	var source = {{s:source}};
	var targetText = {{s:target}};
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

	if (source == "local") {
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
		if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
		var sleep = body.GetComponent<SleepingPills>();
		var anti = body.GetComponent<Antidepressants>();
		var pain = body.GetComponent<Painkillers>();
		return "{\"ok\":true,\"source\":\"local\""
			+ ",\"alive\":" + (body.alive ? "true" : "false")
			+ ",\"conscious\":" + (body.conscious ? "true" : "false")
			+ ",\"brainHealth\":" + num(body.brainHealth)
			+ ",\"consciousness\":" + num(body.consciousness)
			+ ",\"bloodVolume\":" + num(body.bloodVolume)
			+ ",\"bloodOxygen\":" + num(body.bloodOxygen)
			+ ",\"bloodPressure\":" + num(body.bloodPressure)
			+ ",\"heartRate\":" + num(body.heartRate)
			+ ",\"respiratoryRate\":" + num(body.respiratoryRate)
			+ ",\"bloodViscosity\":" + num(body.bloodViscosity)
			+ ",\"fibrillationProgress\":" + num(body.fibrillationProgress)
			+ ",\"fibrillationForced\":" + (body.fibrillationForced ? "true" : "false")
			+ ",\"shock\":" + num(body.shock)
			+ ",\"painShock\":" + num(body.painShock)
			+ ",\"temperature\":" + num(body.temperature)
			+ ",\"hunger\":" + num(body.hunger)
			+ ",\"thirst\":" + num(body.thirst)
			+ ",\"septicShock\":" + num(body.septicShock)
			+ ",\"sicknessAmount\":" + num(body.sicknessAmount)
			+ ",\"stamina\":" + num(body.stamina)
			+ ",\"energy\":" + num(body.energy)
			+ ",\"happiness\":" + num(body.happiness)
			+ ",\"weightOffset\":" + num(body.weightOffset)
			+ ",\"radiationSickness\":" + num(body.radiationSickness)
			+ ",\"disfigured\":" + (body.disfigured ? "true" : "false")
			+ ",\"eyeGone\":" + (body.eyeGone ? "true" : "false")
			+ ",\"bothEyesGone\":" + (body.bothEyesGone ? "true" : "false")
			+ ",\"internalBleeding\":" + num(body.internalBleeding)
			+ ",\"hemothorax\":" + num(body.hemothorax)
			+ ",\"traumaAmount\":" + num(body.traumaAmount)
			+ ",\"dirtyness\":" + num(body.dirtyness)
			+ ",\"wetness\":" + num(body.wetness)
			+ ",\"badSleepAmount\":" + num(body.badSleepAmount)
			+ ",\"hearingLoss\":" + num(body.hearingLoss)
			+ ",\"adrenaline\":" + num(body.adrenaline)
			+ ",\"curAdrenaline\":" + num(body.curAdrenaline)
			+ ",\"venomCurrent\":" + num(body.venomCurrent)
			+ ",\"venomTotal\":" + num(body.venomTotal)
			+ ",\"clawHealth\":" + num(body.clawHealth)
			+ ",\"caffeinated\":" + num(body.caffeinated)
			+ ",\"opiateHappiness\":" + num(body.opiateHappiness)
			+ ",\"antidepressantHappiness\":" + num(body.antidepressantHappiness)
			+ ",\"lastStandTime\":" + num(body.lastStandTime)
			+ ",\"triedRollingLastStand\":" + (body.triedRollingLastStand ? "true" : "false")
			+ ",\"succesfullyRolledLastStand\":" + (body.succesfullyRolledLastStand ? "true" : "false")
			+ ",\"mindwipePresent\":" + (body.mindWipe != null ? "true" : "false")
			+ ",\"mindwipeActive\":" + (body.mindWipe != null && body.mindWipe.active ? "true" : "false")
			+ ",\"sleepingPillsAmount\":" + (sleep != null ? num(sleep.amount) : "0")
			+ ",\"antidepressantsAmount\":" + (anti != null ? num(anti.amount) : "0")
			+ ",\"opiateAmount\":" + (pain != null ? num(pain.opiateAmount) : "0")
			+ "}";
	}

	if (source == "saved") {
		var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
		if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
		var store = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.CharacterData.CharacterDataStore)) as CasualtiesUnknownOnline.Runtime.Session.CharacterData.CharacterDataStore;
		if (store == null) { return "{\"ok\":false,\"error\":\"no-character-store\"}"; }
		var id = System.Convert.ToUInt64(targetText);
		var data = store.GetSavedCharacter(id);
		if (data == null) { return "{\"ok\":false,\"error\":\"no-saved-character\",\"detail\":\"steamId " + targetText + "\"}"; }
		var health = data.Health;
		if (health == null) { return "{\"ok\":false,\"error\":\"no-saved-health\",\"detail\":\"steamId " + targetText + "\"}"; }
		return "{\"ok\":true,\"source\":\"saved\""
			+ ",\"alive\":" + (health.Alive ? "true" : "false")
			+ ",\"conscious\":" + (health.Conscious ? "true" : "false")
			+ ",\"brainHealth\":" + num(health.BrainHealth)
			+ ",\"consciousness\":" + num(health.Consciousness)
			+ ",\"bloodVolume\":" + num(health.BloodVolume)
			+ ",\"bloodOxygen\":" + num(health.BloodOxygen)
			+ ",\"bloodPressure\":" + num(health.BloodPressure)
			+ ",\"heartRate\":" + num(health.HeartRate)
			+ ",\"respiratoryRate\":" + num(health.RespiratoryRate)
			+ ",\"bloodViscosity\":" + num(health.BloodViscosity)
			+ ",\"fibrillationProgress\":" + num(health.FibrillationProgress)
			+ ",\"fibrillationForced\":" + (health.FibrillationForced ? "true" : "false")
			+ ",\"shock\":" + num(health.Shock)
			+ ",\"painShock\":" + num(health.PainShock)
			+ ",\"temperature\":" + num(health.Temperature)
			+ ",\"hunger\":" + num(health.Hunger)
			+ ",\"thirst\":" + num(health.Thirst)
			+ ",\"septicShock\":" + num(health.SepticShock)
			+ ",\"sicknessAmount\":" + num(health.SicknessAmount)
			+ ",\"stamina\":" + num(health.Stamina)
			+ ",\"energy\":" + num(health.Energy)
			+ ",\"happiness\":" + num(health.Happiness)
			+ ",\"weightOffset\":" + num(health.WeightOffset)
			+ ",\"radiationSickness\":" + num(health.RadiationSickness)
			+ ",\"disfigured\":" + (health.Disfigured ? "true" : "false")
			+ ",\"eyeGone\":" + (health.EyeGone ? "true" : "false")
			+ ",\"bothEyesGone\":" + (health.BothEyesGone ? "true" : "false")
			+ ",\"internalBleeding\":" + num(health.InternalBleeding)
			+ ",\"hemothorax\":" + num(health.Hemothorax)
			+ ",\"traumaAmount\":" + num(health.TraumaAmount)
			+ ",\"dirtyness\":" + num(health.Dirtyness)
			+ ",\"wetness\":" + num(health.Wetness)
			+ ",\"badSleepAmount\":" + num(health.BadSleepAmount)
			+ ",\"hearingLoss\":" + num(health.HearingLoss)
			+ ",\"adrenaline\":" + num(health.Adrenaline)
			+ ",\"curAdrenaline\":" + num(health.CurAdrenaline)
			+ ",\"venomCurrent\":" + num(health.VenomCurrent)
			+ ",\"venomTotal\":" + num(health.VenomTotal)
			+ ",\"clawHealth\":" + num(health.ClawHealth)
			+ ",\"caffeinated\":" + num(health.Caffeinated)
			+ ",\"lastStandTime\":" + num(health.LastStandTime)
			+ ",\"triedRollingLastStand\":" + (health.TriedRollingLastStand ? "true" : "false")
			+ ",\"succesfullyRolledLastStand\":" + (health.SuccesfullyRolledLastStand ? "true" : "false")
			+ ",\"mindwipePresent\":" + (health.MindwipeScriptPresent ? "true" : "false")
			+ ",\"mindwipeActive\":" + (health.MindwipeScriptActive ? "true" : "false")
			+ ",\"sleepingPillsAmount\":" + num(health.SleepingPillsAmount)
			+ ",\"antidepressantsAmount\":" + num(health.AntidepressantsAmount)
			+ ",\"opiateAmount\":" + num(health.OpiateAmount)
			+ "}";
	}

	return "{\"ok\":false,\"error\":\"bad-source\",\"detail\":\"" + source + "\"}";
}))()
