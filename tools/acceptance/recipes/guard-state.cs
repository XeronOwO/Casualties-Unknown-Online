// recipe: guard-state
// args: none
// returns: ok, hostVitalsActive, skipAudioClip, callDepth, callOrigin, error, detail
//
// Reads the three static guards that decide whether a `Sound.Play` call is ALLOWED to make a sound at all, from the running client:
// `HostVitalsPass.IsActive` (both sound patches return false while it is set), `SoundCaptureContext.SkipAudioClipCapture` (the string patch's skip flag)
// and the call-context depth/origin. A replay that is refused by one of these looks exactly like a replay that never happened, which is why the peer's
// side of the lockpick-pain ticket needs them as values rather than an assumption.
((System.Func<string>)(() => {
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	var vitals = "unknown";
	var skip = "unknown";
	var depth = "unknown";
	var origin = "unknown";
	for (var i = 0; i < assemblies.Length; i++) {
		var vitalsType = (System.Type)null;
		try { vitalsType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Patches.HostVitalsPass", false); } catch { vitalsType = null; }
		if (vitalsType != null) {
			var property = vitalsType.GetProperty("IsActive", flags);
			if (property != null) { vitals = System.Convert.ToBoolean(property.GetValue(null)) ? "true" : "false"; }
			break;
		}
	}
	for (var i = 0; i < assemblies.Length; i++) {
		var captureType = (System.Type)null;
		try { captureType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Patches.SoundCaptureContext", false); } catch { captureType = null; }
		if (captureType != null) {
			var property = captureType.GetProperty("SkipAudioClipCapture", flags);
			if (property != null) { skip = System.Convert.ToBoolean(property.GetValue(null)) ? "true" : "false"; }
			break;
		}
	}
	for (var i = 0; i < assemblies.Length; i++) {
		var contextType = (System.Type)null;
		try { contextType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.CallContext", false); } catch { contextType = null; }
		if (contextType != null) {
			var depthField = contextType.GetField("_depth", flags);
			var currentProperty = contextType.GetProperty("Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
			if (depthField != null) { depth = System.Convert.ToInt32(depthField.GetValue(null)).ToString(System.Globalization.CultureInfo.InvariantCulture); }
			if (currentProperty != null) { origin = currentProperty.GetValue(null).ToString(); }
			break;
		}
	}

	return "{\"ok\":true,\"hostVitalsActive\":\"" + vitals + "\",\"skipAudioClip\":\"" + skip + "\",\"callDepth\":\"" + depth + "\",\"callOrigin\":\"" + origin + "\"}";
}))()
