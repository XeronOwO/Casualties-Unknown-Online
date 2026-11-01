// recipe: call-context-state
// args: none
// returns: ok, skipAudioClipCapture, depth, current, refused, refusedOrigins, maxDepth, error, detail
//
// Reads the scope stack the character-sound capture depends on: how deep it is right now, which origin is innermost, and — the part that cannot be seen any
// other way — HOW MANY scopes the stack REFUSED. `CallContext.Enter` stops accepting scopes at `MaxDepth` and records each refusal instead of throwing
// (src/CasualtiesUnknownOnline.GameAdapter/CallContext.cs), so a refused scope leaves the enclosing origin as `Current`: a sound played inside it is then
// classified as whatever the caller was, or not classified at all. That is invisible from every other recipe, and it is exactly the shape of a one-shot
// that the acting client plays while no report is ever sent.
//
// The counters are read by reflection because the class is internal to the adapter, matching how the other recipes reach adapter state.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type type = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { type = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.CallContext", false); } catch { type = null; }
		if (type != null) { break; }
	}
	if (type == null) { return "{\"ok\":false,\"error\":\"no-call-context\"}"; }

	var depthField = type.GetField("_depth", flags);
	var refusedField = type.GetField("_refused", flags);
	var refusedArray = type.GetField("Refused", flags);
	var maxDepthField = type.GetField("MaxDepth", flags);
	var currentProp = type.GetProperty("Current", flags);
	if (currentProp == null) { currentProp = type.GetProperty("Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic); }

	var depth = depthField == null ? -1 : System.Convert.ToInt32(depthField.GetValue(null));
	var refused = refusedField == null ? -1 : System.Convert.ToInt32(refusedField.GetValue(null));
	var maxDepth = maxDepthField == null ? -1 : System.Convert.ToInt32(maxDepthField.GetValue(null));
	var current = currentProp == null ? "unknown" : currentProp.GetValue(null).ToString();
	var origins = "";
	if (refusedArray != null) {
		var array = refusedArray.GetValue(null) as System.Array;
		if (array != null) {
			for (var i = 0; i < array.Length; i++) {
				if (origins.Length > 0) { origins += "|"; }
				origins += array.GetValue(i).ToString();
			}
		}
	}

	var skipField = (System.Type)null;
	var skipValue = "unknown";
	for (var j = 0; j < assemblies.Length; j++) {
		try { skipField = assemblies[j].GetType("CasualtiesUnknownOnline.GameAdapter.Patches.SoundCaptureContext", false); } catch { skipField = null; }
		if (skipField != null) { break; }
	}
	if (skipField != null) {
		var prop = skipField.GetProperty("SkipAudioClipCapture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		if (prop != null) { skipValue = (System.Convert.ToBoolean(prop.GetValue(null)) ? "true" : "false"); }
	}
	return "{\"ok\":true,\"skipAudioClipCapture\":\"" + skipValue + "\",\"depth\":" + depth.ToString(inv)
		+ ",\"current\":\"" + current + "\""
		+ ",\"refused\":" + refused.ToString(inv)
		+ ",\"refusedOrigins\":\"" + origins + "\""
		+ ",\"maxDepth\":" + maxDepth.ToString(inv) + "}";
}))()
