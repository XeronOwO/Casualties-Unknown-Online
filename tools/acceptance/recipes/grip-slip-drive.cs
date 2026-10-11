// recipe: grip-slip-drive
// args: mode=s limb=n target=s piece=n
// serves: cross-player-item-use-random-determinism
// returns: ok, mode, error, detail, step, exception, message, minigameLimbNull, minigameLimbName,
//          limbBodyNull, displayBodyNull, minigameLimbIsDisplayLimb, currentlyHeldNull (plus the per-mode
//          fields below) — a sequence step that threw answers error=step-failed and names the seam it was in.
//          The step-failed answer carries no `mode`/`detail`, and the scene fields beside the step name were
//          added after the failures that motivated them, so they are a diagnosis aid, not evidence.
//
// Drives ONE bare-handed shrapnel grip slip on the operator's client, which is the only way the
// cross-player grip-slip rows can be judged: the game reaches `ShrapnelMinigame.BreakGrasp` from its own
// `Update` on mouse motion, and this project drives no OS-level input.
//
// The four steps are separate modes so a run can verify each seam before the next one, and so a failure
// names WHICH seam it was:
//   mode=probe           report the reflection path itself: the patch bridge, the domain field that holds
//                        the session, and the session's member list (steamId + inWorld per member)
//   mode=open  target=<steamid>
//                        open the remote medical view through PRODUCTION's own entry and report what its
//                        guards saw
//   mode=start limb=<n>  start the shared shrapnel special session on that limb of the displayed body
//   mode=break piece=<n> put the native minigame into the held state on that piece and call its public
//                        `BreakGrasp()`, which is what the patch's window wraps
//
// WHY THE VIEW IS OPENED BY THE PRODUCTION ENTRY: the first cut of this recipe built the focus by hand —
// `RemoteMedicalView.Open(<the peer's LIVE render clone>)` — because the run's member probe read the
// member's `InWorld` as a PROPERTY while `MemberPresenceTable.MemberPresence.InWorld` is a FIELD, so the
// probe reported `false` for every peer and the shortcut looked necessary. It was not, and it cost a
// batch's worth of misdiagnosis (2026-10-11, batch `20261011-b`): the focus then pointed at a body the
// game is running, CUO's limb-component codec added the game's OWN `SplintLimb` to it, the game's own
// `Start` registered a wound-view icon whose predicate dereferences the limb, and the focus's own
// teardown destroyed that clone — a `NullReferenceException` every frame while the panel stayed open.
// The view is therefore opened only through `RemoteMedicalCoordinator.Open`, and a refusal is reported
// with the guard readings that produced it.
//
// WHY REFLECTION: everything below is `internal` to the plugin and reached today only from a real mouse
// gesture. `PatchBridge.Impl` is the one static seam, and it exposes the LOCAL id (`LocalSteamId`) but no
// peer's — `SessionSurface` carries two UI flags and nothing else — so the peer identity comes from the
// session's member list, reached through the bridge's own domain object. The one game field this writes is
// the minigame's held slot (`mode=break`, the state a real drag would have produced).
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var limbIndex = (int){{n:limb}};
	var pieceIndex = (int){{n:piece}};
	var targetText = {{s:target}};
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
	var sessionField = domains.GetType().GetField("Session", flags);
	if (sessionField == null) { return "{\"ok\":false,\"error\":\"no-session-field\"}"; }
	var session = sessionField.GetValue(domains);
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }

	// `InWorld` is a FIELD on the member record; reading it as a property reported `false` for every peer
	// and sent this recipe down a shortcut that produced a false defect. Both shapes are read here, and the
	// production predicate is reported beside it so a reader never has to re-derive the rule.
	var isRemoteInWorld = session.GetType().GetMethod("IsRemoteInWorld", flags);

	if (mode == "probe") {
		var membersProp = session.GetType().GetProperty("Members", flags);
		var report = "{\"ok\":true,\"mode\":\"probe\",\"bridge\":\"" + impl.GetType().Name + "\",\"domainsField\":\"" + domainsField.Name
			+ "\",\"session\":\"" + session.GetType().Name + "\",\"members\":[";
		var memberTypeName = "?";
		var members = membersProp != null ? membersProp.GetValue(session, null) as System.Collections.IEnumerable : null;
		var first = true;
		if (members != null) {
			foreach (var member in members) {
				if (memberTypeName == "?") { memberTypeName = member.GetType().FullName; }
				var idProp = member.GetType().GetProperty("SteamId", flags);
				if (idProp == null) { idProp = member.GetType().GetProperty("Id", flags); }
				var idField = idProp == null ? member.GetType().GetField("SteamId", flags) : null;
				var id = idProp != null ? idProp.GetValue(member, null) : (idField != null ? idField.GetValue(member) : null);
				var inWorldProp = member.GetType().GetProperty("InWorld", flags);
				var inWorldField = member.GetType().GetField("InWorld", flags);
				var inWorld = inWorldProp != null ? inWorldProp.GetValue(member, null) : (inWorldField != null ? inWorldField.GetValue(member) : null);
				var production = false;
				if (isRemoteInWorld != null && id != null) {
					try { production = (bool)isRemoteInWorld.Invoke(session, new object[] { System.Convert.ToUInt64(id.ToString()) }); } catch { production = false; }
				}
				if (!first) { report += ","; }
				first = false;
				report += "{\"steamId\":\"" + (id != null ? id.ToString() : "?") + "\",\"inWorld\":" + (inWorld is bool b && b ? "true" : "false")
					+ ",\"isRemoteInWorld\":" + (production ? "true" : "false") + "}";
			}
		}
		var localProp = impl.GetType().GetProperty("LocalSteamId", flags);
		var local = localProp != null ? localProp.GetValue(impl, null) : null;
		return report + "],\"memberType\":\"" + memberTypeName + "\",\"localSteamId\":\"" + (local != null ? local.ToString() : "?") + "\"}";
	}

	if (mode == "patches") {
		// Decisive check: is the grip-slip patch actually attached to the native method? Every guard the
		// patch asks reported true and the authority still logged nothing, so the question is whether the
		// prefix/postfix run AT ALL.
		var breakMethod = typeof(ShrapnelMinigame).GetMethod("BreakGrasp", flags);
		if (breakMethod == null) { return "{\"ok\":false,\"error\":\"no-breakgrasp-method\"}"; }
		var report = "{\"ok\":true,\"mode\":\"patches\",\"prefixes\":[";
		var patchInfo = HarmonyLib.Harmony.GetPatchInfo(breakMethod);
		var firstEntry = true;
		if (patchInfo != null) {
			foreach (var patch in patchInfo.Prefixes) {
				if (!firstEntry) { report += ","; }
				firstEntry = false;
				report += "\"" + patch.owner + ":" + (patch.PatchMethod != null ? patch.PatchMethod.DeclaringType.Name + "." + patch.PatchMethod.Name : "?") + "\"";
			}
		}
		report += "],\"postfixes\":[";
		firstEntry = true;
		if (patchInfo != null) {
			foreach (var patch in patchInfo.Postfixes) {
				if (!firstEntry) { report += ","; }
				firstEntry = false;
				report += "\"" + patch.owner + ":" + (patch.PatchMethod != null ? patch.PatchMethod.DeclaringType.Name + "." + patch.PatchMethod.Name : "?") + "\"";
			}
		}
		report += "],\"finalizers\":[";
		firstEntry = true;
		if (patchInfo != null) {
			foreach (var patch in patchInfo.Finalizers) {
				if (!firstEntry) { report += ","; }
				firstEntry = false;
				report += "\"" + patch.owner + "\"";
			}
		}
		return report + "]}";
	}

	var viewType = (System.Type)null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { viewType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalView", false); } catch { viewType = null; }
		if (viewType != null) { break; }
	}
	if (viewType == null) { return "{\"ok\":false,\"error\":\"no-medical-view-type\"}"; }
	var displayProp = viewType.GetProperty("DisplayBody", flags);
	var isOpenProp = viewType.GetProperty("IsOpen", flags);

	if (mode == "open") {
		var remoteMedicalField = domains.GetType().GetField("RemoteMedical", flags);
		if (remoteMedicalField == null) { return "{\"ok\":false,\"error\":\"no-remote-medical-field\"}"; }
		var remoteMedical = remoteMedicalField.GetValue(domains);
		var openMethod = remoteMedical.GetType().GetMethod("Open", flags);
		if (openMethod == null) { return "{\"ok\":false,\"error\":\"no-open-method\"}"; }

		// The production guards, read BEFORE the call so a refusal names itself instead of being reported as
		// a bare `false`: they are the coordinator's own conditions.
		var localInWorldProp = session.GetType().GetProperty("LocalInWorld", flags);
		var sessionActiveProp = session.GetType().GetProperty("SessionActive", flags);
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
		var display = displayProp != null ? displayProp.GetValue(null, null) as Body : null;
		var displayName = display != null ? display.name : "null";
		return "{\"ok\":true,\"mode\":\"open\",\"opened\":" + (opened ? "true" : "false")
			+ ",\"isOpen\":" + (isOpen is bool o && o ? "true" : "false")
			+ ",\"target\":\"" + targetText + "\""
			+ ",\"displayBodyName\":\"" + displayName + "\""
			+ ",\"sessionActive\":" + (sessionActiveProp != null && sessionActiveProp.GetValue(session, null) is bool sa && sa ? "true" : "false")
			+ ",\"localInWorld\":" + (localInWorldProp != null && localInWorldProp.GetValue(session, null) is bool lw && lw ? "true" : "false")
			+ ",\"isRemoteInWorld\":" + (remoteInWorld ? "true" : "false")
			+ ",\"hasSnapshot\":" + (hasSnapshot ? "true" : "false")
			+ ",\"hasHealth\":" + (hasHealth ? "true" : "false") + "}";
	}

	// The focus must ALREADY be open, and it must be the display copy the previous call opened: calling
	// `Open` here would run `Close` first (cancelling the very session this step belongs to). A missing
	// view is a refusal with its own name, never a hand-built focus.
	var displayBody = displayProp != null ? displayProp.GetValue(null, null) as Body : null;
	if (displayBody == null) { return "{\"ok\":false,\"error\":\"no-display-body\",\"detail\":\"the remote medical view is not open on this client — run mode=open in its own call first\"}"; }
	if (limbIndex < 0 || limbIndex >= displayBody.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\",\"detail\":\"limb " + limbIndex + " of " + displayBody.limbs.Length + "\"}"; }
	var targetLimb = displayBody.limbs[limbIndex];
	if (targetLimb == null) { return "{\"ok\":false,\"error\":\"no-such-limb\",\"detail\":\"limb " + limbIndex + "\"}"; }
	// The witness that the focus is PRODUCTION's own display copy and not a live body: the
	// `RemoteMedicalDisplayBody` marker the coordinator puts on the copy's ROOT — the same fact the codec's
	// own display guard reads. The type is internal to the plugin, so it is reached by name; a missing type
	// answers "not a display copy" rather than passing by accident. (The Body sits on a child of the cloned
	// root, which is why the marker is read from the root rather than from the body's own name.)
	var markerType = (System.Type)null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { markerType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalDisplayBody", false); } catch { markerType = null; }
		if (markerType != null) { break; }
	}
	if (markerType == null || displayBody.transform.root.GetComponent(markerType) == null) {
		return "{\"ok\":false,\"error\":\"focus-is-not-a-display-copy\",\"detail\":\"display body root is \\\"" + displayBody.transform.root.name + "\\\" and carries no RemoteMedicalDisplayBody marker\"}";
	}

	if (mode == "start") {
		var startMethod = impl.GetType().GetMethod("TryStartRemoteShrapnelSpecial", flags);
		if (startMethod == null) { return "{\"ok\":false,\"error\":\"no-start-method\"}"; }
		var started = (bool)startMethod.Invoke(impl, new object[] { targetLimb });
		return "{\"ok\":true,\"mode\":\"start\",\"started\":" + (started ? "true" : "false")
			+ ",\"limb\":" + limbIndex + ",\"displayBodyName\":\"" + displayBody.transform.root.name + "\""
			+ ",\"shrapnel\":" + targetLimb.shrapnel
			+ ",\"hasShrapnel\":" + (targetLimb.hasShrapnel ? "true" : "false") + "}";
	}

	if (mode == "break") {
		var minigameBase = MinigameBase.main;
		if (minigameBase == null) { return "{\"ok\":false,\"error\":\"no-minigame-base\"}"; }
		var minigame = minigameBase.currentMinigame as ShrapnelMinigame;
		if (minigame == null) { return "{\"ok\":false,\"error\":\"no-shrapnel-minigame\",\"detail\":\"currentMinigame is " + (minigameBase.currentMinigame == null ? "null" : minigameBase.currentMinigame.GetType().Name) + "\"}"; }
		var objectsField = typeof(ShrapnelMinigame).GetField("objects", flags);
		var heldField = typeof(ShrapnelMinigame).GetField("currentlyHeld", flags);
		if (objectsField == null || heldField == null) { return "{\"ok\":false,\"error\":\"no-minigame-fields\"}"; }
		var objects = objectsField.GetValue(minigame) as System.Collections.Generic.List<UnityEngine.RectTransform>;
		if (objects == null || pieceIndex < 0 || pieceIndex >= objects.Count) { return "{\"ok\":false,\"error\":\"piece-out-of-range\",\"detail\":\"piece " + pieceIndex + "\"}"; }
		// A break-grasp is only accepted from the piece's OWNER, and ownership is set by a grab report.
		// A real slip always follows a grab — the patch posts one every frame a piece is held — but a
		// driver that sets the held slot and breaks inside the same call would post none.
		var updateType = (System.Type)null;
		var handlerType = (System.Type)null;
		for (var i = 0; i < assemblies.Length; i++) {
			if (updateType == null) { try { updateType = assemblies[i].GetType("CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.ShrapnelPieceUpdate", false); } catch { updateType = null; } }
			if (handlerType == null) { try { handlerType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.RemoteMedicalOperationHandler", false); } catch { handlerType = null; } }
			if (updateType != null && handlerType != null) { break; }
		}
		if (updateType == null || handlerType == null) { return "{\"ok\":false,\"error\":\"no-report-types\"}"; }
		var grab = System.Activator.CreateInstance(updateType);
		updateType.GetProperty("PieceIndex").SetValue(grab, pieceIndex, null);
		updateType.GetProperty("Grabbed").SetValue(grab, true, null);
		updateType.GetProperty("OwnershipChange").SetValue(grab, true, null);
		var reportMethod = handlerType.GetMethod("ReportShrapnelUpdate", flags);
		if (reportMethod == null) { return "{\"ok\":false,\"error\":\"no-report-method\"}"; }
		var before = displayBody.limbs[limbIndex];
		var skinBefore = before.skinHealth;
		var bleedBefore = before.bleedAmount;
		var painBefore = before.pain;
		// Every step below is named, and the whole sequence reports WHICH one threw: an exception inside
		// the native call or inside the plugin's own seams used to leave the eval with a bare internal
		// error and no seam named, which is the one thing this recipe exists to avoid.
		var tryBeginText = "no-method";
		var step = "start";
		try {
			step = "grab-report";
			reportMethod.Invoke(null, new object[] { grab });
			step = "hold-piece";
			heldField.SetValue(minigame, objects[pieceIndex]);
			// Probe the patch's own entry point with the very argument the patch would pass, so a null
			// return names ITS cause rather than leaving "the patch ran but said nothing" as the answer.
			step = "try-begin-probe";
			var tryBeginMethod = handlerType.GetMethod("TryBeginGripSlip", flags);
			if (tryBeginMethod != null) {
				var probeSlip = tryBeginMethod.Invoke(null, new object[] { minigame });
				tryBeginText = probeSlip == null ? "null" : "slip";
				if (probeSlip != null) {
					var closeMethod = probeSlip.GetType().GetMethod("Close", flags);
					if (closeMethod != null) { closeMethod.Invoke(probeSlip, null); }
				}
			}
			step = "break-grasp";
			minigame.BreakGrasp();
			// DIAGNOSTIC: report the slip the way the patch would have. If the authority's own line
			// appears after this, the patch did not run and the authority's chain is fine; if it does
			// not, the refusal is downstream. The authority's message names which it was.
			step = "slip-report";
			var slipReport = System.Activator.CreateInstance(updateType);
			updateType.GetProperty("PieceIndex").SetValue(slipReport, pieceIndex, null);
			updateType.GetProperty("BreakGrasp").SetValue(slipReport, true, null);
			updateType.GetProperty("OwnershipChange").SetValue(slipReport, true, null);
			reportMethod.Invoke(null, new object[] { slipReport });
		} catch (System.Exception stepError) {
			var inner = stepError;
			if (stepError is System.Reflection.TargetInvocationException && stepError.InnerException != null) { inner = stepError.InnerException; }
			// The scene at the failure, read through the same fields the native call dereferences: a
			// minigame holding a limb whose display body has since been rebuilt is the difference between
			// "the native call is broken" and "this drive started the session against a body that was
			// replaced under it", and the two must not be reported as the same thing.
			var limbField = typeof(ShrapnelMinigame).GetField("limb", flags);
			var minigameLimb = limbField != null ? limbField.GetValue(minigame) as Limb : null;
			var displayNow = displayProp != null ? displayProp.GetValue(null, null) as Body : null;
			var sameLimb = false;
			if (minigameLimb != null && displayNow != null && limbIndex >= 0 && limbIndex < displayNow.limbs.Length) {
				sameLimb = System.Object.ReferenceEquals(minigameLimb, displayNow.limbs[limbIndex]);
			}
			var limbName = "null";
			try { if (minigameLimb != null) { limbName = minigameLimb.name; } } catch { limbName = "destroyed"; }
			return "{\"ok\":false,\"error\":\"step-failed\",\"step\":\"" + step + "\",\"exception\":\"" + inner.GetType().FullName
				+ "\",\"message\":\"" + inner.Message.Replace("\\", "/").Replace("\"", "'").Replace("\r", " ").Replace("\n", " ") + "\""
				+ ",\"tryBegin\":\"" + tryBeginText + "\""
				+ ",\"minigameLimbNull\":" + (minigameLimb == null ? "true" : "false")
				+ ",\"minigameLimbName\":\"" + limbName + "\""
				+ ",\"limbBodyNull\":" + (minigameLimb == null || minigameLimb.body == null ? "true" : "false")
				+ ",\"displayBodyNull\":" + (displayNow == null ? "true" : "false")
				+ ",\"minigameLimbIsDisplayLimb\":" + (sameLimb ? "true" : "false")
				+ ",\"currentlyHeldNull\":" + (heldField.GetValue(minigame) == null ? "true" : "false") + "}";
		}
		var after = displayBody.limbs[limbIndex];
		var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
		// The send side's own state, so a silent no-op names its cause: the static handler keeps the
		// active session AND the sender delegate, and ReportShrapnelUpdate does nothing (and logs
		// nothing) when either is unusable. Guarded: a diagnostic must never be the thing that fails.
		var activeOpIdText = "?";
		var senderPresent = false;
		try {
			var opsType = (System.Type)null;
			for (var i = 0; i < assemblies.Length; i++) {
				try { opsType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.RemoteShrapnelOperationHandler", false); } catch { opsType = null; }
				if (opsType != null) { break; }
			}
			if (opsType != null) {
				var activeField = opsType.GetField("_activeShrapnel", flags);
				var senderField = opsType.GetField("_shrapnelUpdateSender", flags);
				var activeSession = activeField != null ? activeField.GetValue(null) : null;
				var senderDelegate = senderField != null ? senderField.GetValue(null) : null;
				senderPresent = senderDelegate != null;
				if (activeSession == null) { activeOpIdText = "no-active-session"; }
				else {
					// The CLIENT-side session type is RemoteShrapnelUseSession, whose OperationId is a
					// PROPERTY; reading it as a field is what produced a false "no-opid-field" before.
					var opIdProp = activeSession.GetType().GetProperty("OperationId", flags);
					var opIdField = activeSession.GetType().GetField("OperationId", flags);
					if (opIdProp != null) { activeOpIdText = opIdProp.GetValue(activeSession, null).ToString(); }
					else if (opIdField != null) { activeOpIdText = opIdField.GetValue(activeSession).ToString(); }
					else { activeOpIdText = "no-opid:" + activeSession.GetType().Name; }
				}
			} else { activeOpIdText = "no-handler-type"; }
		} catch (System.Exception diagError) { activeOpIdText = "diag:" + diagError.GetType().Name; }
		var viewOpen = isOpenProp != null ? isOpenProp.GetValue(null, null) : null;
		var activeMethod = handlerType.GetMethod("IsActiveShrapnelMinigame", flags);
		var minigameActive = activeMethod != null ? activeMethod.Invoke(null, new object[] { minigame }) : null;
		return "{\"ok\":true,\"mode\":\"break\",\"piece\":" + pieceIndex + ",\"limb\":" + limbIndex
			+ ",\"displayBodyName\":\"" + displayBody.transform.root.name + "\""
			+ ",\"viewOpen\":" + (viewOpen is bool vo && vo ? "true" : "false")
			+ ",\"minigameActive\":" + (minigameActive is bool ma && ma ? "true" : "false")
			+ ",\"activeOpId\":\"" + activeOpIdText + "\""
			+ ",\"senderPresent\":" + (senderPresent ? "true" : "false")
			+ ",\"tryBegin\":\"" + tryBeginText + "\""
			+ ",\"skinDelta\":" + num(skinBefore - after.skinHealth)
			+ ",\"bleedDelta\":" + num(after.bleedAmount - bleedBefore)
			+ ",\"painDelta\":" + num(after.pain - painBefore) + "}";
	}

	return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"" + mode + "\"}";
}))()
