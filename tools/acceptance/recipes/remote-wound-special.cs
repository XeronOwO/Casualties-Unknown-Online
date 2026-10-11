// recipe: remote-wound-special
// args: mode=s target=s limb=n
// serves: medical-state-names-no-kind, medical-removal-limb-component-not-applied
// returns: ok, mode, target, limb, started, viewOpen, displayBodyName, opened, sessionActive, localInWorld,
//          isRemoteInWorld, hasSnapshot, hasHealth, shrapnel, hasShrapnel, splinted, dislocated,
//          dismembered, components, error, detail
//
// Drives ONE remote WoundView special action on a peer's limb through the plugin's own seam
// (`GameAdapterBridge.TryStartRemoteWoundSpecial`), which is the counterpart of the native
// `PlayerCamera.TryPerformSpecialUIAction` wound-view branch. The family dispatches by the DISPLAY limb's
// own state: tourniquet removal, the shared shrapnel session, splint removal, then dislocation fix — so a
// run states the limb and reports which state it found, never a guessed action.
//
// TWO steps, and they are two calls on purpose:
//   mode=open     open the view through PRODUCTION's own entry (`RemoteMedicalCoordinator.Open`) and
//                 report what its guards saw. The coordinator applies the peer's limb COMPONENT state to
//                 the display body on its own tick, so the limb a dispatch sees right after an open is not
//                 yet the limb the peer carries — a dispatch in the same call reads "carries no splint" and
//                 refuses (observed: started=false with components="" on a limb the peer had splinted).
//   mode=special  dispatch on the limb the view already shows. It requires the focus the open left behind
//                 and refuses by name when it is gone: calling `Open` here would run `Close` first, which
//                 cancels sessions and destroys the display body whose components this step dispatches on.
//
// WHY THE VIEW IS OPENED BY THE PRODUCTION ENTRY: an earlier cut built the focus by hand from the peer's
// LIVE render clone, because the run's member probe read the member's `InWorld` as a PROPERTY while
// `MemberPresenceTable.MemberPresence.InWorld` is a FIELD — so it reported `false` for every peer and the
// shortcut looked necessary. It was not: the focus then pointed at a body the game is running, CUO's
// limb-component codec added the game's OWN `SplintLimb` to it, and the game's own `Start` registered a
// wound-view icon whose predicate dereferences the limb (batch `20261011-b`'s per-frame exception storm).
// The `displayBodyName` field is the witness: a display copy is named `MedicalDisplay_<steamid>`.
//
// The call returns as soon as the REQUEST is sent. The authority answers asynchronously (it asks the
// target's client before it commits), so the outcome is judged from the logs and from the target's own
// body, never from this return value.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var targetText = {{s:target}};
	var limbIndex = (int){{n:limb}};
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

	var bridgeType = (System.Type)null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		try { bridgeType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.PatchBridge", false); } catch { bridgeType = null; }
		if (bridgeType != null) { break; }
	}
	if (bridgeType == null) { return "{\"ok\":false,\"error\":\"no-patch-bridge-type\"}"; }
	var implProp = bridgeType.GetProperty("Impl", flags);
	if (implProp == null) { return "{\"ok\":false,\"error\":\"no-impl-property\"}"; }
	var impl = implProp.GetValue(null, null);
	if (impl == null) { return "{\"ok\":false,\"error\":\"no-bridge-instance\"}"; }

	// The bridge's own domain object, found by TYPE rather than by field name: a private field renamed
	// would otherwise break this silently.
	System.Reflection.FieldInfo domainsField = null;
	var bridgeFields = impl.GetType().GetFields(flags);
	for (var i = 0; i < bridgeFields.Length; i++) {
		if (bridgeFields[i].FieldType.Name == "GameAdapterDomains") { domainsField = bridgeFields[i]; break; }
	}
	if (domainsField == null) { return "{\"ok\":false,\"error\":\"no-domains-field\",\"detail\":\"bridge " + impl.GetType().FullName + "\"}"; }
	var domains = domainsField.GetValue(impl);

	var viewType = (System.Type)null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { viewType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalView", false); } catch { viewType = null; }
		if (viewType != null) { break; }
	}
	if (viewType == null) { return "{\"ok\":false,\"error\":\"no-medical-view-type\"}"; }
	var isOpenProp = viewType.GetProperty("IsOpen", flags);
	var displayProp = viewType.GetProperty("DisplayBody", flags);

	if (mode == "open") {
		var remoteMedicalField = domains.GetType().GetField("RemoteMedical", flags);
		if (remoteMedicalField == null) { return "{\"ok\":false,\"error\":\"no-remote-medical-field\"}"; }
		var remoteMedical = remoteMedicalField.GetValue(domains);
		var openMethod = remoteMedical.GetType().GetMethod("Open", flags);
		if (openMethod == null) { return "{\"ok\":false,\"error\":\"no-open-method\"}"; }

		var sessionField = domains.GetType().GetField("Session", flags);
		var session = sessionField != null ? sessionField.GetValue(domains) : null;
		var isRemoteInWorld = session != null ? session.GetType().GetMethod("IsRemoteInWorld", flags) : null;
		var localInWorldProp = session != null ? session.GetType().GetProperty("LocalInWorld", flags) : null;
		var sessionActiveProp = session != null ? session.GetType().GetProperty("SessionActive", flags) : null;
		var remoteInWorld = false;
		if (isRemoteInWorld != null) {
			try { remoteInWorld = (bool)isRemoteInWorld.Invoke(session, new object[] { System.Convert.ToUInt64(targetText) }); } catch { remoteInWorld = false; }
		}
		var characterDataField = domains.GetType().GetField("CharacterDataSync", flags);
		var hasSnapshot = false;
		var hasHealth = false;
		if (characterDataField != null) {
			var characterData = characterDataField.GetValue(domains);
			var cloneDataProp = characterData != null ? characterData.GetType().GetProperty("CloneData", flags) : null;
			var cloneData = cloneDataProp != null ? cloneDataProp.GetValue(characterData, null) as System.Collections.IDictionary : null;
			var key = System.Convert.ToUInt64(targetText);
			if (cloneData != null && cloneData.Contains(key)) {
				hasSnapshot = true;
				var snapshot = cloneData[key];
				var healthProp = snapshot != null ? snapshot.GetType().GetProperty("Health", flags) : null;
				hasHealth = healthProp != null && healthProp.GetValue(snapshot, null) != null;
			}
		}

		var opened = (bool)openMethod.Invoke(remoteMedical, new object[] { System.Convert.ToUInt64(targetText), "target" });
		var isOpen = isOpenProp != null ? isOpenProp.GetValue(null, null) : null;
		var openedBody = displayProp != null ? displayProp.GetValue(null, null) as Body : null;
		var openedName = openedBody != null ? openedBody.name : "null";
		return "{\"ok\":true,\"mode\":\"open\",\"target\":\"" + targetText + "\""
			+ ",\"opened\":" + (opened ? "true" : "false")
			+ ",\"viewOpen\":" + (isOpen is bool vo && vo ? "true" : "false")
			+ ",\"displayBodyName\":\"" + openedName + "\""
			+ ",\"sessionActive\":" + (sessionActiveProp != null && sessionActiveProp.GetValue(session, null) is bool sa && sa ? "true" : "false")
			+ ",\"localInWorld\":" + (localInWorldProp != null && localInWorldProp.GetValue(session, null) is bool lw && lw ? "true" : "false")
			+ ",\"isRemoteInWorld\":" + (remoteInWorld ? "true" : "false")
			+ ",\"hasSnapshot\":" + (hasSnapshot ? "true" : "false")
			+ ",\"hasHealth\":" + (hasHealth ? "true" : "false")
			+ ",\"started\":false,\"limb\":" + limbIndex + ",\"shrapnel\":0,\"hasShrapnel\":false"
			+ ",\"splinted\":false,\"dislocated\":false,\"dismembered\":false,\"components\":\"\"}";
	}

	if (mode != "special") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"" + mode + "\"}"; }

	// The settled display body the previous call's open left behind — never re-created here.
	var display = displayProp != null ? displayProp.GetValue(null, null) as Body : null;
	if (display == null) { return "{\"ok\":false,\"error\":\"no-display-body\",\"detail\":\"the remote medical view is not open on this client — run mode=open in its own call first\"}"; }
	if (limbIndex < 0 || limbIndex >= display.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\",\"detail\":\"limb " + limbIndex + " of " + display.limbs.Length + "\"}"; }
	var limb = display.limbs[limbIndex];
	if (limb == null) { return "{\"ok\":false,\"error\":\"no-such-limb\",\"detail\":\"limb " + limbIndex + "\"}"; }
	if (!display.name.StartsWith("MedicalDisplay_", System.StringComparison.Ordinal)) {
		return "{\"ok\":false,\"error\":\"focus-is-not-a-display-copy\",\"detail\":\"display body is \\\"" + display.name + "\\\", not a MedicalDisplay_ copy\"}";
	}

	var startMethod = impl.GetType().GetMethod("TryStartRemoteWoundSpecial", flags);
	if (startMethod == null) { return "{\"ok\":false,\"error\":\"no-wound-special-method\"}"; }
	var started = (bool)startMethod.Invoke(impl, new object[] { limb });

	var componentText = "";
	if (limb.GetComponent<SplintLimb>() != null) { componentText = "SplintLimb"; }
	if (limb.GetComponent<TourniquetScript>() != null) { componentText = componentText.Length > 0 ? componentText + ",TourniquetScript" : "TourniquetScript"; }
	var viewOpen = isOpenProp != null ? isOpenProp.GetValue(null, null) : null;
	return "{\"ok\":true,\"mode\":\"special\",\"target\":\"" + targetText + "\",\"limb\":" + limbIndex
		+ ",\"started\":" + (started ? "true" : "false")
		+ ",\"viewOpen\":" + (viewOpen is bool open && open ? "true" : "false")
		+ ",\"displayBodyName\":\"" + display.name + "\""
		+ ",\"shrapnel\":" + limb.shrapnel
		+ ",\"hasShrapnel\":" + (limb.hasShrapnel ? "true" : "false")
		+ ",\"splinted\":" + (limb.splinted ? "true" : "false")
		+ ",\"dislocated\":" + (limb.dislocated ? "true" : "false")
		+ ",\"dismembered\":" + (limb.dismembered ? "true" : "false")
		+ ",\"components\":\"" + componentText + "\"}";
}))()
