// recipe: medication-component-probe
// args: mode=s
//        mode is one of: plant-sleepingpills, plant-mindwipe, read, clear-local. Each token on the args
//        line above must be <key>=s or <key>=n, so the allowed values are named here instead.
// serves: todo/heal-command-limb-and-hollow-restore.md — the owed runtime row for the removal direction:
//         an apply path that only ADDED left every receiver's stale copy standing, and nothing in the suite
//         would fail if the destroy arms were deleted, so this is what proves it over the real wire.
// returns: ok, mode, plus on read: sleepingPills, antidepressants, mindwipe, mindwipeActive, painkillers
//          (each true/false) and bodyName; on plant: what was planted and the amount it carries.
//
// Plants the exact stale state the old apply path used to leave behind — a component on the LOCAL body that
// the OWNER of that body does not have — and then reads whether the next authoritative snapshot removes it.
// The host in a fresh run carries no medication, so its snapshot is the "absent" case by construction; that is
// what makes the reading meaningful rather than a fixture that agrees with itself.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }

	var sleeping = body.GetComponent<SleepingPills>();
	var antidepressants = body.GetComponent<Antidepressants>();
	var mindwipe = body.GetComponent<MindwipeScript>();
	var painkillers = body.GetComponent<Painkillers>();
	var culture = System.Globalization.CultureInfo.InvariantCulture;
	var state = "\"sleepingPills\":" + (sleeping != null ? "true" : "false")
		+ ",\"antidepressants\":" + (antidepressants != null ? "true" : "false")
		+ ",\"mindwipe\":" + (mindwipe != null ? "true" : "false")
		+ ",\"mindwipeActive\":" + (mindwipe != null && mindwipe.active ? "true" : "false")
		+ ",\"painkillers\":" + (painkillers != null ? "true" : "false")
		+ ",\"bodyName\":\"" + body.gameObject.name + "\"";

	if (mode == "read") {
		return "{\"ok\":true,\"mode\":\"read\"," + state + "}";
	}

	if (mode == "plant-sleepingpills") {
		// 300 is the game's own additive dose (Liquids.cs: `+= 300f * …`), so the planted copy is the shape a
		// real dose leaves behind rather than a value this probe invented.
		if (sleeping == null) { sleeping = body.gameObject.AddComponent<SleepingPills>(); }
		sleeping.amount = 300f;
		return "{\"ok\":true,\"mode\":\"plant-sleepingpills\",\"amount\":300," + state + "}";
	}

	if (mode == "plant-mindwipe") {
		// `active` is the moodle's condition while the component's mere PRESENCE is what zeroes effective
		// happiness, so planting it active covers the state a player actually feels.
		if (mindwipe == null) { mindwipe = body.gameObject.AddComponent<MindwipeScript>(); }
		mindwipe.active = true;
		return "{\"ok\":true,\"mode\":\"plant-mindwipe\",\"active\":true," + state + "}";
	}

	if (mode == "clear-local") {
		// The owner's own side of the same fact: remove it here and let the snapshot carry "absent" out, which
		// is the direction the heal's hollow half needs.
		if (sleeping != null) { UnityEngine.Object.Destroy(sleeping); }
		if (mindwipe != null) { UnityEngine.Object.Destroy(mindwipe); }
		return "{\"ok\":true,\"mode\":\"clear-local\"," + state + "}";
	}

	return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be plant-sleepingpills, plant-mindwipe, clear-local or read\"}";
}))()
