// recipe: console-enter
// args: target=n
// serves: ticket console-enter-on-empty-does-not-close row 1 - Enter on an EMPTY console must close it, and this runs the overlay's own
//         Enter rule (ConsoleInputSession.SubmitOrClose) in-process so a batch can read the console's visibility before and after without
//         an OS-level key press. Open the console first with -Action console-ui.
// returns: ok, wasOpen, input, isOpenAfter, error, detail
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
	var marker = (int)({{n:target}});
	object plugin = null;
	foreach (var entry in BepInEx.Bootstrap.Chainloader.PluginInfos) {
		var instance = entry.Value.Instance;
		if (instance != null && instance.GetType().FullName == "CasualtiesUnknownOnline.Plugin") { plugin = instance; break; }
	}
	if (plugin == null) { return "{\"ok\":false,\"error\":\"no-plugin\"}"; }
	var component = plugin as UnityEngine.Component;
	if (component == null) { return "{\"ok\":false,\"error\":\"not-a-component\"}"; }
	object pluginHost = null;
	var behaviours = component.gameObject.GetComponents(typeof(UnityEngine.MonoBehaviour));
	for (var i = 0; i < behaviours.Length; i++) {
		var candidate = behaviours[i];
		if (candidate != null && candidate.GetType().FullName == "CasualtiesUnknownOnline.PluginHost") { pluginHost = candidate; break; }
	}
	if (pluginHost == null) { return "{\"ok\":false,\"error\":\"no-plugin-host\"}"; }
	var overlayField = pluginHost.GetType().GetField("_onlineUi", Flags);
	var overlay = overlayField == null ? null : overlayField.GetValue(pluginHost);
	// Two hops: PluginHost._onlineUi → OnlineUiOverlay, whose own _onlineUi field is the object that owns the console overlay.
	var innerField = overlay == null ? null : overlay.GetType().GetField("_onlineUi", Flags);
	var inner = innerField == null ? null : innerField.GetValue(overlay);
	var consoleField = inner == null ? null : inner.GetType().GetField("_commandOverlay", Flags);
	var console = consoleField == null ? null : consoleField.GetValue(inner);
	var sessionField = console == null ? null : console.GetType().GetField("_session", Flags);
	var session = sessionField == null ? null : sessionField.GetValue(console);
	if (session == null) { return "{\"ok\":false,\"error\":\"no-console-session\"}"; }
	var openProperty = session.GetType().GetProperty("IsOpen", Flags);
	var inputProperty = session.GetType().GetProperty("Input", Flags);
	var submitOrClose = session.GetType().GetMethod("SubmitOrClose", Flags);
	if (openProperty == null || submitOrClose == null) { return "{\"ok\":false,\"error\":\"no-submit-or-close\"}"; }
	var wasOpen = (bool)openProperty.GetValue(session, null);
	var input = (inputProperty == null ? "" : (inputProperty.GetValue(session, null) as string)) ?? "";
	// The opener pre-fills the command prefix; row 1 is ENTER ON AN EMPTY BOX, so the field is cleared first — the state a player reaches
	// by deleting the prefix, which is the only way an open console can be empty.
	var setInput = session.GetType().GetMethod("SetInput", Flags);
	if (setInput != null) {
		var trimmed = input.Trim();
		if (trimmed.Length > 0) { setInput.Invoke(session, new object[] { "", null }); }
	}
	var emptied = (inputProperty == null ? "" : (inputProperty.GetValue(session, null) as string)) ?? "";
	var consumed = (bool)submitOrClose.Invoke(session, null);
	var nowOpen = (bool)openProperty.GetValue(session, null);
	return "{\"ok\":true"
		+ ",\"marker\":" + marker.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"wasOpen\":" + (wasOpen ? "true" : "false")
		+ ",\"input\":\"" + input + "\""
		+ ",\"emptied\":\"" + emptied + "\""
		+ ",\"consumed\":" + (consumed ? "true" : "false")
		+ ",\"isOpenAfter\":" + (nowOpen ? "true" : "false")
		+ "}";
}))()
