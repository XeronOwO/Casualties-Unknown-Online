// recipe: medication-component-probe
// args: mode=s target=s
//        mode is plant-sleepingpills | plant-mindwipe | read | clear-local; target is local | display.
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
	var target = {{s:target}};
	Body body = null;
	if (target == "display") {
		// The remote medical view inspects a DISPLAY BODY named for the inspected player, parked off-scene and
		// under an inactive hierarchy, so FindObjectsOfType would not see it - the all-objects form does.
		var all = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(Body));
		for (var i = 0; i < all.Length; i++) {
			var candidate = all[i] as Body;
			// Found by its MARKER COMPONENT, not by name: the coordinator does name the object
			// `MedicalDisplay_{steamId:X}`, but a listing of the scene showed three bodies all called plain
			// `Body`, so something in the game resets the name and a name match silently finds nothing.
			// By component TYPE NAME, not by the type itself: the marker is internal to the adapter, and an
			// in-process eval cannot reference it - naming it made the whole recipe fail to compile, which
			// reads as "every mode stopped answering" rather than as one broken mode.
			if (candidate == null) { continue; }
			// The marker goes on the instantiated ROOT while the Body comes from GetComponentInChildren, so
			// when the template keeps its Body on a child the two sit on DIFFERENT GameObjects - which is
			// precisely what a search keyed on the Body's own object misses, and both the earlier name match
			// and the marker match made that mistake. Walk the ancestor chain instead.
			var node = candidate.transform;
			while (node != null) {
				var parts = node.GetComponents<UnityEngine.Component>();
				for (var c = 0; c < parts.Length; c++) {
					var part = parts[c];
					if (part != null && part.GetType().Name == "RemoteMedicalDisplayBody") { body = candidate; break; }
				}
				if (body != null) { break; }
				node = node.parent;
			}
			if (body != null) { break; }
		}
	} else if (target == "remote") {
		// The body another client holds for a REMOTE player, found by the driver component CUO adds to
		// it - by type NAME, because that type is internal to the adapter and an in-process eval cannot
		// reference it, the same lesson the display target already learned.
		var all = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(Body));
		for (var i = 0; i < all.Length; i++) {
			var candidate = all[i] as Body;
			if (candidate == null || !candidate.gameObject.activeInHierarchy) { continue; }
			var parts = candidate.GetComponents<UnityEngine.Component>();
			for (var c = 0; c < parts.Length; c++) {
				if (parts[c] != null && parts[c].GetType().Name == "RemoteBodyDriver") { body = candidate; break; }
			}
			if (body != null) { break; }
		}
	} else {
		body = camera.body;
	}
	if (body == null) {
		// LIST rather than report an absence: a "no such body" from a lookup that has not shown what it CAN
		// see is the shape this cycle keeps meeting. The answer carries every Body the lookup returned, with
		// each one inactive-marked, so the next reading distinguishes "the call finds nothing" from "it finds
		// bodies and none is named that".
		var every = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(Body));
		var names = new System.Collections.Generic.List<string>();
		for (var k = 0; k < every.Length; k++) {
			var seen = every[k] as Body;
			if (seen == null) { continue; }
			// Each body carries its component type names, because "found three bodies, none matched" cannot say
			// WHICH part of a body the marker lives on or whether it is there at all.
			var kinds = new System.Collections.Generic.List<string>();
			var comps = seen.GetComponents<UnityEngine.Component>();
			for (var c = 0; c < comps.Length; c++) { if (comps[c] != null) { kinds.Add(comps[c].GetType().Name); } }
			names.Add(seen.gameObject.name + (seen.gameObject.activeInHierarchy ? "" : "(inactive)") + " [" + string.Join(",", kinds.ToArray()) + "]");
		}
		return "{\"ok\":false,\"error\":\"no-body\",\"target\":\"" + target + "\",\"bodiesFound\":" + every.Length.ToString() + ",\"bodies\":[\"" + string.Join("\",\"", names.ToArray()) + "\"]}";
	}

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
		+ ",\"bodyName\":\"" + body.gameObject.name + "\""
		+ ",\"target\":\"" + target + "\"";

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

	if (mode == "limb") {
		// The list of limbs with the name the game's own `amputate` command resolves (`Body.LimbByName`) and the
		// flags a restore has to change, so a run picks a REAL limb name instead of guessing one.
		var limbs = body.limbs;
		var parts = new System.Collections.Generic.List<string>();
		for (var i = 0; i < limbs.Length; i++) {
			var limb = limbs[i];
			if (limb == null) { parts.Add("{\"i\":" + i + ",\"name\":\"<destroyed>\"}"); continue; }
			parts.Add("{\"i\":" + i
				+ ",\"name\":\"" + limb.fullName + "\""
				// The name `Body.LimbByName` resolves is the object's own, not the localized display name: passing
				// `fullName` to the game's `amputate` throws "is not a valid limb" and reads as a missing feature.
				+ ",\"objectName\":\"" + limb.gameObject.name + "\""
				+ ",\"dismembered\":" + (limb.dismembered ? "true" : "false")
				+ ",\"active\":" + (limb.gameObject.activeSelf ? "true" : "false")
				+ ",\"muscle\":" + limb.muscleHealth.ToString("0.#", culture) + "}");
		}
		return "{\"ok\":true,\"mode\":\"limb\",\"standing\":" + (body.standing ? "true" : "false")
			+ ",\"shock\":" + body.shock.ToString("0.#", culture)
			+ ",\"limbs\":[" + string.Join(",", parts.ToArray()) + "]}";
	}
	return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be plant-sleepingpills, plant-mindwipe, clear-local or read\"}";
}))()
