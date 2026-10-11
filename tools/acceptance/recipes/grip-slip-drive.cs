// recipe: grip-slip-drive
// args: mode=s limb=n target=s piece=n
// serves: cross-player-item-use-random-determinism
// returns: ok, mode, error, detail (plus per-mode fields below)
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
//                        open the remote medical view on that member's displayed body
//   mode=start limb=<n>  start the shared shrapnel special session on that limb of the displayed body
//   mode=break piece=<n> put the native minigame into the held state on that piece and call its public
//                        `BreakGrasp()`, which is what the patch's window wraps
//
// WHY REFLECTION: everything below is `internal` to the plugin and reached today only from a real mouse
// gesture. `PatchBridge.Impl` is the one static seam, and it exposes the LOCAL id (`LocalSteamId`) but no
// peer's — `SessionSurface` carries two UI flags and nothing else — so the peer identity comes from the
// session's member list, reached through the bridge's own domain object. Nothing here writes a game or
// plugin field except the minigame's held slot, which is the state a real drag would have produced.
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

	if (mode == "probe") {
		var membersProp = session.GetType().GetProperty("Members", flags);
		var report = "{\"ok\":true,\"mode\":\"probe\",\"bridge\":\"" + impl.GetType().Name + "\",\"domainsField\":\"" + domainsField.Name
			+ "\",\"session\":\"" + session.GetType().Name + "\",\"members\":[";
		var memberTypeName = "?";
		var memberShape = "";
		var members = membersProp != null ? membersProp.GetValue(session, null) as System.Collections.IEnumerable : null;
		var first = true;
		if (members != null) {
			foreach (var member in members) {
				if (memberTypeName == "?") {
					memberTypeName = member.GetType().FullName;
					var parts = "";
					var props = member.GetType().GetProperties(flags);
					for (var i = 0; i < props.Length; i++) { parts += props[i].Name + " "; }
					var fields = member.GetType().GetFields(flags);
					for (var i = 0; i < fields.Length; i++) { parts += fields[i].Name + " "; }
					memberShape = parts;
				}
				var idProp = member.GetType().GetProperty("SteamId", flags);
				if (idProp == null) { idProp = member.GetType().GetProperty("Id", flags); }
				var idField = idProp == null ? member.GetType().GetField("SteamId", flags) : null;
				var id = idProp != null ? idProp.GetValue(member, null) : (idField != null ? idField.GetValue(member) : null);
				var inWorldProp = member.GetType().GetProperty("InWorld", flags);
				var inWorld = inWorldProp != null ? inWorldProp.GetValue(member, null) : null;
				if (!first) { report += ","; }
				first = false;
				report += "{\"steamId\":\"" + (id != null ? id.ToString() : "?") + "\",\"inWorld\":" + (inWorld is bool b && b ? "true" : "false") + "}";
			}
		}
		var localProp = impl.GetType().GetProperty("LocalSteamId", flags);
		var local = localProp != null ? localProp.GetValue(impl, null) : null;
		return report + "],\"localSteamId\":\"" + (local != null ? local.ToString() : "?") + "\"}";
	}

	var viewType = (System.Type)null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { viewType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalView", false); } catch { viewType = null; }
		if (viewType != null) { break; }
	}
	if (viewType == null) { return "{\"ok\":false,\"error\":\"no-medical-view-type\"}"; }

	if (mode == "open" || mode == "start" || mode == "auto") {
		var rendererField = domains.GetType().GetField("Renderer", flags);
		if (rendererField == null) { return "{\"ok\":false,\"error\":\"no-renderer-field\"}"; }
		var renderer = rendererField.GetValue(domains);
		var tryGet = renderer.GetType().GetMethod("TryGetRemoteBody", flags);
		if (tryGet == null) { return "{\"ok\":false,\"error\":\"no-try-get-remote-body\"}"; }
		var args = new object[] { System.Convert.ToUInt64(targetText), null };
		var found = (bool)tryGet.Invoke(renderer, args);
		var body = args[1] as Body;
		if (!found || body == null) { return "{\"ok\":false,\"error\":\"no-remote-body\",\"detail\":\"steamId " + targetText + "\"}"; }
		// The NATIVE panel must be open before the focus is set. Setting the focus alone is not enough:
		// the coordinator's pump tears a remote focus down whenever the native panel is closed, and that
		// teardown cancels an in-flight shrapnel session — which is exactly what a driver that only set
		// the static focus produced (session created and cancelled in the same millisecond).
		var camera = PlayerCamera.main;
		if (camera == null) { return "{\"ok\":false,\"error\":\"no-player-camera\"}"; }
		if (camera.woundView == null) { return "{\"ok\":false,\"error\":\"no-wound-view-object\"}"; }
		if (!camera.woundView.activeSelf) { camera.WoundViewButton(); }
		if (!camera.woundView.activeSelf) { return "{\"ok\":false,\"error\":\"native-wound-view-refused\",\"detail\":\"the local body is not alive or available\"}"; }
		var nativeView = WoundView.view;
		if (nativeView != null) { nativeView.body = body; }
		var openMethod = viewType.GetMethod("Open", flags);
		if (openMethod == null) { return "{\"ok\":false,\"error\":\"no-open-method\"}"; }
		openMethod.Invoke(null, new object[] { body, System.Convert.ToUInt64(targetText), "target" });
		var isOpenProp = viewType.GetProperty("IsOpen", flags);
		var isOpen = isOpenProp != null ? isOpenProp.GetValue(null, null) : null;
		if (mode == "open") { return "{\"ok\":true,\"mode\":\"open\",\"isOpen\":" + (isOpen is bool o && o ? "true" : "false") + ",\"target\":\"" + targetText + "\"}"; }
	}

	// Read the display body in the SAME call that opened it: the view is torn down between driver
	// calls (the native panel toggle and the focus's own lifecycle both close it), so a run cannot open
	// it in one call and start a session in the next.
	var displayProp = viewType.GetProperty("DisplayBody", flags);
	var display = displayProp != null ? displayProp.GetValue(null, null) as Body : null;
	if (display == null) { return "{\"ok\":false,\"error\":\"no-display-body\",\"detail\":\"the remote medical view is not open\"}"; }
	if (limbIndex < 0 || limbIndex >= display.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\",\"detail\":\"limb " + limbIndex + " of " + display.limbs.Length + "\"}"; }
	var targetLimb = display.limbs[limbIndex];
	if (targetLimb == null) { return "{\"ok\":false,\"error\":\"no-such-limb\",\"detail\":\"limb " + limbIndex + "\"}"; }

	if (mode == "start" || mode == "auto") {
		var startMethod = impl.GetType().GetMethod("TryStartRemoteShrapnelSpecial", flags);
		if (startMethod == null) { return "{\"ok\":false,\"error\":\"no-start-method\"}"; }
		var started = (bool)startMethod.Invoke(impl, new object[] { targetLimb });
		if (mode == "start") {
			return "{\"ok\":true,\"mode\":\"start\",\"started\":" + (started ? "true" : "false")
				+ ",\"limb\":" + limbIndex + ",\"shrapnel\":" + targetLimb.shrapnel
				+ ",\"hasShrapnel\":" + (targetLimb.hasShrapnel ? "true" : "false") + "}";
		}
		if (!started) { return "{\"ok\":false,\"error\":\"start-refused\"}"; }
	}

	if (mode == "break" || mode == "auto") {
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
		reportMethod.Invoke(null, new object[] { grab });
		heldField.SetValue(minigame, objects[pieceIndex]);
		var before = display.limbs[limbIndex];
		var skinBefore = before.skinHealth;
		var bleedBefore = before.bleedAmount;
		var painBefore = before.pain;
		minigame.BreakGrasp();
		// DIAGNOSTIC: report the slip the way the patch would have. If the authority's own line appears
		// after this, the patch did not run and the authority's chain is fine; if it does not, the refusal
		// is downstream. The authority's message names which it was.
		var slipReport = System.Activator.CreateInstance(updateType);
		updateType.GetProperty("PieceIndex").SetValue(slipReport, pieceIndex, null);
		updateType.GetProperty("BreakGrasp").SetValue(slipReport, true, null);
		updateType.GetProperty("OwnershipChange").SetValue(slipReport, true, null);
		reportMethod.Invoke(null, new object[] { slipReport });
		var after = display.limbs[limbIndex];
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
					var opIdField = activeSession.GetType().GetField("OperationId", flags);
					activeOpIdText = opIdField != null ? opIdField.GetValue(activeSession).ToString() : "no-opid-field";
				}
			} else { activeOpIdText = "no-handler-type"; }
		} catch (System.Exception diagError) { activeOpIdText = "diag:" + diagError.GetType().Name; }
		var isOpenNow = viewType.GetProperty("IsOpen", flags);
		var viewOpen = isOpenNow != null ? isOpenNow.GetValue(null, null) : null;
		var activeMethod = handlerType.GetMethod("IsActiveShrapnelMinigame", flags);
		var minigameActive = activeMethod != null ? activeMethod.Invoke(null, new object[] { minigame }) : null;
		return "{\"ok\":true,\"mode\":\"break\",\"piece\":" + pieceIndex + ",\"limb\":" + limbIndex
			+ ",\"viewOpen\":" + (viewOpen is bool vo && vo ? "true" : "false")
			+ ",\"minigameActive\":" + (minigameActive is bool ma && ma ? "true" : "false")
			+ ",\"activeOpId\":\"" + activeOpIdText + "\""
			+ ",\"senderPresent\":" + (senderPresent ? "true" : "false")
			+ ",\"skinDelta\":" + num(skinBefore - after.skinHealth)
			+ ",\"bleedDelta\":" + num(after.bleedAmount - bleedBefore)
			+ ",\"painDelta\":" + num(after.pain - painBefore) + "}";
	}

	return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"" + mode + "\"}";
}))()
