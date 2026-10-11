// recipe: remote-wound-special
// args: mode=s target=s limb=n
// serves: medical-state-names-no-kind
// returns: ok, mode, target, limb, started, viewOpen, shrapnel, hasShrapnel, splinted, dislocated, dismembered,
//          components, error, detail
//
// Drives ONE remote WoundView special action on a peer's limb through the plugin's own seam
// (`GameAdapterBridge.TryStartRemoteWoundSpecial`), which is the counterpart of the native
// `PlayerCamera.TryPerformSpecialUIAction` wound-view branch. The family dispatches by the DISPLAY limb's
// own state: tourniquet removal, the shared shrapnel session, splint removal, then dislocation fix — so a
// run states the limb and reports which state it found, never a guessed action.
//
// TWO steps, and they are two calls on purpose:
//   mode=open     open the native panel and the remote medical view on that peer, and report the display
//                 limb as it stands. The coordinator applies the peer's limb COMPONENT state to the
//                 display body on its own tick, so the limb a dispatch sees right after an open is not yet
//                 the limb the peer carries — a dispatch in the same call reads "carries no splint" and
//                 refuses (observed: started=false with components="" on a limb the peer had splinted).
//   mode=special  dispatch on the limb the view already shows. It restores the focus by writing its two
//                 fields — never by calling `Open`, which calls `Close` first — so the settled display
//                 body and the peer's limb components on it survive the call gap. A missing view is
//                 re-focused, never re-created: the body has to be the one the coordinator already
//                 applied the components to.
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
	var targetProp = viewType.GetProperty("TargetSteamId", flags);
	var displayProp = viewType.GetProperty("DisplayBody", flags);

	if (mode == "special") {
		// The coordinator's pump drops a remote focus between two driver calls, and the settled display
		// body is what carries the peer's limb components — so the focus is restored by WRITING its two
		// fields (the same shape grip-slip-drive's break uses) rather than by calling Open, which calls
		// Close first and would cancel sessions and toggle the native panel off.
		var focusRendererField = domains.GetType().GetField("Renderer", flags);
		if (focusRendererField == null) { return "{\"ok\":false,\"error\":\"no-renderer-field\"}"; }
		var focusRenderer = focusRendererField.GetValue(domains);
		var focusTryGet = focusRenderer.GetType().GetMethod("TryGetRemoteBody", flags);
		if (focusTryGet == null) { return "{\"ok\":false,\"error\":\"no-try-get-remote-body\"}"; }
		var focusArgs = new object[] { System.Convert.ToUInt64(targetText), null };
		var focusFound = (bool)focusTryGet.Invoke(focusRenderer, focusArgs);
		var focusBody = focusArgs[1] as Body;
		if (!focusFound || focusBody == null) { return "{\"ok\":false,\"error\":\"no-remote-body\",\"detail\":\"steamId " + targetText + "\"}"; }
		var focusCamera = PlayerCamera.main;
		if (focusCamera == null) { return "{\"ok\":false,\"error\":\"no-player-camera\"}"; }
		if (focusCamera.woundView == null) { return "{\"ok\":false,\"error\":\"no-wound-view-object\"}"; }
		if (!focusCamera.woundView.activeSelf) { focusCamera.WoundViewButton(); }
		if (!focusCamera.woundView.activeSelf) { return "{\"ok\":false,\"error\":\"native-wound-view-refused\",\"detail\":\"the local body is not alive or available\"}"; }
		var focusView = WoundView.view;
		if (focusView != null) { focusView.body = focusBody; }
		var focusBodyField = viewType.GetField("_displayBody", flags);
		var focusTargetField = viewType.GetField("_targetSteamId", flags);
		if (focusBodyField == null || focusTargetField == null) { return "{\"ok\":false,\"error\":\"no-focus-fields\"}"; }
		focusBodyField.SetValue(null, focusBody);
		focusTargetField.SetValue(null, System.Convert.ToUInt64(targetText));
	} else if (mode != "open") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"" + mode + "\"}";
	}

	if (mode == "open") {
		var rendererField = domains.GetType().GetField("Renderer", flags);
		if (rendererField == null) { return "{\"ok\":false,\"error\":\"no-renderer-field\"}"; }
		var renderer = rendererField.GetValue(domains);
		var tryGet = renderer.GetType().GetMethod("TryGetRemoteBody", flags);
		if (tryGet == null) { return "{\"ok\":false,\"error\":\"no-try-get-remote-body\"}"; }
		var bodyArgs = new object[] { System.Convert.ToUInt64(targetText), null };
		var found = (bool)tryGet.Invoke(renderer, bodyArgs);
		var openBody = bodyArgs[1] as Body;
		if (!found || openBody == null) { return "{\"ok\":false,\"error\":\"no-remote-body\",\"detail\":\"steamId " + targetText + "\"}"; }

		var camera = PlayerCamera.main;
		if (camera == null) { return "{\"ok\":false,\"error\":\"no-player-camera\"}"; }
		if (camera.woundView == null) { return "{\"ok\":false,\"error\":\"no-wound-view-object\"}"; }
		if (!camera.woundView.activeSelf) { camera.WoundViewButton(); }
		if (!camera.woundView.activeSelf) { return "{\"ok\":false,\"error\":\"native-wound-view-refused\",\"detail\":\"the local body is not alive or available\"}"; }
		var nativeView = WoundView.view;
		if (nativeView != null) { nativeView.body = openBody; }
		var openMethod = viewType.GetMethod("Open", flags);
		if (openMethod == null) { return "{\"ok\":false,\"error\":\"no-open-method\"}"; }
		openMethod.Invoke(null, new object[] { openBody, System.Convert.ToUInt64(targetText), "target" });
	}

	var display = displayProp != null ? displayProp.GetValue(null, null) as Body : null;
	if (display == null) { return "{\"ok\":false,\"error\":\"no-display-body\",\"detail\":\"the remote medical view is not open\"}"; }
	if (limbIndex < 0 || limbIndex >= display.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\",\"detail\":\"limb " + limbIndex + " of " + display.limbs.Length + "\"}"; }
	var limb = display.limbs[limbIndex];
	if (limb == null) { return "{\"ok\":false,\"error\":\"no-such-limb\",\"detail\":\"limb " + limbIndex + "\"}"; }

	var started = false;
	if (mode == "special") {
		var startMethod = impl.GetType().GetMethod("TryStartRemoteWoundSpecial", flags);
		if (startMethod == null) { return "{\"ok\":false,\"error\":\"no-wound-special-method\"}"; }
		started = (bool)startMethod.Invoke(impl, new object[] { limb });
	}

	var componentText = "";
	if (limb.GetComponent<SplintLimb>() != null) { componentText = "SplintLimb"; }
	if (limb.GetComponent<TourniquetScript>() != null) { componentText = componentText.Length > 0 ? componentText + ",TourniquetScript" : "TourniquetScript"; }
	var viewOpen = isOpenProp != null ? isOpenProp.GetValue(null, null) : null;
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"target\":\"" + targetText + "\",\"limb\":" + limbIndex
		+ ",\"started\":" + (started ? "true" : "false")
		+ ",\"viewOpen\":" + (viewOpen is bool vo && vo ? "true" : "false")
		+ ",\"shrapnel\":" + limb.shrapnel
		+ ",\"hasShrapnel\":" + (limb.hasShrapnel ? "true" : "false")
		+ ",\"splinted\":" + (limb.splinted ? "true" : "false")
		+ ",\"dislocated\":" + (limb.dislocated ? "true" : "false")
		+ ",\"dismembered\":" + (limb.dismembered ? "true" : "false")
		+ ",\"components\":\"" + componentText + "\"}";
}))()
