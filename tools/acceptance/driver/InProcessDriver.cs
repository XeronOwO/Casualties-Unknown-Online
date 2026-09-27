// The in-process half of tools/acceptance/drive-in-process.ps1.
//
// The PowerShell caller substitutes COMMAND, ARGUMENT and TEXT and sends this file as one HotRepl eval
// snippet; the snippet returns one JSON object as a string. It never uses OS-level input: it enters the
// live Online UI at the frame's registered action table, handing the real control ids the same intent
// payloads the native surface view emits, so the plugin's own policies (LobbySwitchActions and the
// native run-start gate) decide.
//
// Written for the Mono.CSharp evaluator the BepInEx host selects: C# 7.x, no usings, fully qualified
// names, and one top-level expression whose value is the result. The evaluator's REPL cannot emit a
// closure for a lambda nested in another lambda, so every helper below is CAPTURE-FREE: its state
// travels as a parameter, or as a const (which the compiler inlines). That is a limit of the evaluator,
// not a style choice — a capturing inner lambda fails at run time.

(new System.Func<string>(() => {
	var command = "{{COMMAND}}";
	var argument = "{{ARGUMENT}}";
	var text = "{{TEXT}}";

	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags staticFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

	// ---- JSON output --------------------------------------------------------------------------
	var escape = new System.Func<string, string>((value) => {
		if (value == null) { return ""; }
		var builder = new System.Text.StringBuilder();
		for (var i = 0; i < value.Length; i++) {
			var c = value[i];
			if (c == '\\' || c == '"') { builder.Append('\\').Append(c); }
			else if (c == '\n') { builder.Append("\\n"); }
			else if (c == '\r') { builder.Append("\\r"); }
			else if (c == '\t') { builder.Append("\\t"); }
			else if (c < ' ') { builder.Append(' '); }
			else { builder.Append(c); }
		}
		return builder.ToString();
	});
	var finish = new System.Func<System.Collections.Generic.List<string>, string>((list) => "{" + string.Join(",", list.ToArray()) + "}");
	var putText = new System.Action<System.Collections.Generic.List<string>, System.Func<string, string>, string, string>((list, esc, key, value) => list.Add("\"" + esc(key) + "\":\"" + esc(value) + "\""));
	var putRaw = new System.Action<System.Collections.Generic.List<string>, System.Func<string, string>, string, string>((list, esc, key, value) => list.Add("\"" + esc(key) + "\":" + value));
	var fail = new System.Func<System.Func<string, string>, string, string, string>((esc, code, detail) => "{\"ok\":false,\"error\":\"" + esc(code) + "\",\"detail\":\"" + esc(detail) + "\"}");
	var quoteList = new System.Func<System.Func<string, string>, System.Collections.Generic.List<string>, string>((esc, list) => {
		var items = new string[list.Count];
		for (var i = 0; i < list.Count; i++) { items[i] = "\"" + esc(list[i]) + "\""; }
		return "[" + string.Join(",", items) + "]";
	});

	// ---- reflection access --------------------------------------------------------------------
	var findField = new System.Func<object, string, object>((target, name) => {
		if (target == null) { return null; }
		var field = target.GetType().GetField(name, instanceFlags);
		return field == null ? null : field.GetValue(target);
	});
	var findProperty = new System.Func<object, string, object>((target, name) => {
		if (target == null) { return null; }
		var property = target.GetType().GetProperty(name, instanceFlags);
		return property == null || !property.CanRead ? null : property.GetValue(target, null);
	});
	var findMethodWith = new System.Func<object, string, int, System.Reflection.MethodInfo>((target, name, parameterCount) => {
		if (target == null) { return null; }
		var methods = target.GetType().GetMethods(instanceFlags);
		for (var i = 0; i < methods.Length; i++) {
			if (methods[i].Name == name && methods[i].GetParameters().Length == parameterCount) { return methods[i]; }
		}
		return null;
	});
	var readBool = new System.Func<object, string, bool>((target, name) => {
		if (target == null) { return false; }
		var field = target.GetType().GetField(name, instanceFlags);
		if (field == null) { return false; }
		var value = field.GetValue(target);
		return value == null ? false : System.Convert.ToBoolean(value);
	});
	var pageName = new System.Func<object, string>((state) => {
		if (state == null) { return ""; }
		var field = state.GetType().GetField("Page", instanceFlags);
		if (field == null) { return ""; }
		var page = field.GetValue(state);
		return page == null ? "" : page.ToString();
	});
	var controlIds = new System.Func<System.Collections.IDictionary, System.Collections.Generic.List<string>>((table) => {
		var ids = new System.Collections.Generic.List<string>();
		if (table != null) {
			foreach (var key in table.Keys) { ids.Add(System.Convert.ToString(key)); }
			ids.Sort(System.StringComparer.Ordinal);
		}
		return ids;
	});
	var unwrap = new System.Func<System.Exception, string>((thrown) => {
		var inner = thrown is System.Reflection.TargetInvocationException && thrown.InnerException != null
			? thrown.InnerException
			: thrown;
		return inner.GetType().Name + ": " + inner.Message;
	});

	// The live CUO plugin: the Online UI host is constructed by the plugin and never registered in the
	// container, so the plugin object is the one handle to it. BepInEx's own table answers first; a
	// scene scan is the fallback for a host whose table cannot be read.
	object plugin = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && plugin == null; i++) {
		System.Type chainloader = null;
		try { chainloader = assemblies[i].GetType("BepInEx.Bootstrap.Chainloader", false); }
		catch { chainloader = null; }
		var infosProperty = chainloader == null ? null : chainloader.GetProperty("PluginInfos", staticFlags);
		var infos = infosProperty == null ? null : (infosProperty.GetValue(null, null) as System.Collections.IDictionary);
		if (infos == null) { continue; }
		foreach (System.Collections.DictionaryEntry entry in infos) {
			var instance = findProperty(entry.Value, "Instance") ?? findField(entry.Value, "Instance");
			if (instance != null && instance.GetType().FullName == "CasualtiesUnknownOnline.Plugin") { plugin = instance; break; }
		}
	}
	if (plugin == null) {
		var behaviours = UnityEngine.Object.FindObjectsOfType(typeof(UnityEngine.MonoBehaviour), true);
		for (var i = 0; i < behaviours.Length; i++) {
			var behaviour = behaviours[i];
			if (behaviour == null) { continue; } // Unity object ==
			if (behaviour.GetType().FullName == "CasualtiesUnknownOnline.Plugin") { plugin = behaviour; break; }
		}
	}

	var host = findField(plugin, "_onlineUi");
	var overlay = findField(host, "_onlineUi");
	var window = findProperty(overlay, "Window");
	var windowState = findProperty(window, "State");
	var actions = findField(overlay, "_actions") as System.Collections.IDictionary;

	// The container's public services: the facts this driver reports, and the role the launcher
	// click would carry.
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	var steam = services == null ? null : (services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Steam.SteamService)) as CasualtiesUnknownOnline.Runtime.Steam.SteamService);
	var session = services == null ? null : (services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService);
	var presence = services == null ? null : (services.GetService(typeof(CasualtiesUnknownOnline.Runtime.GameAdapter.IWorldPresenceQuery)) as CasualtiesUnknownOnline.Runtime.GameAdapter.IWorldPresenceQuery);
	var gate = services == null ? null : (services.GetService(typeof(CasualtiesUnknownOnline.Runtime.GameAdapter.IStartGateState)) as CasualtiesUnknownOnline.Runtime.GameAdapter.IStartGateState);

	var fields = new System.Collections.Generic.List<string>();

	// ---- commands -----------------------------------------------------------------------------
	if (command == "ping") {
		if (plugin == null || overlay == null) {
			return fail(escape, "no-cuo-online-ui", "the HotRepl evaluator answered but the CUO Online UI is not reachable in this process");
		}
		putRaw(fields, escape, "ok", "true");
		putRaw(fields, escape, "command", "\"" + escape(command) + "\"");
		putRaw(fields, escape, "plugin", "true");
		putRaw(fields, escape, "overlay", "true");
		putRaw(fields, escape, "steamInit", steam != null && steam.IsInitialized ? "true" : "false");
		putRaw(fields, escape, "controlCount", (actions == null ? 0 : actions.Count).ToString());
		return finish(fields);
	}

	if (command == "state") {
		putRaw(fields, escape, "ok", "true");
		putText(fields, escape, "command", command);
		putRaw(fields, escape, "plugin", plugin != null ? "true" : "false");
		putRaw(fields, escape, "overlay", overlay != null ? "true" : "false");
		putRaw(fields, escape, "windowVisible", readBool(windowState, "Visible") ? "true" : "false");
		putText(fields, escape, "page", pageName(windowState));
		putText(fields, escape, "lobbyIdInput", System.Convert.ToString(findField(windowState, "LobbyIdInput")));
		putText(fields, escape, "transport", System.Convert.ToString(findField(windowState, "TransportMode")));
		putText(fields, escape, "lobby", steam == null ? "0" : steam.CurrentLobbyId.ToString());
		putText(fields, escape, "role", session == null ? "" : session.Role.ToString());
		putRaw(fields, escape, "active", session != null && session.SessionActive ? "true" : "false");
		putText(fields, escape, "hostId", session == null ? "0" : session.HostSteamId.ToString());
		putRaw(fields, escape, "steamInit", steam != null && steam.IsInitialized ? "true" : "false");
		putRaw(fields, escape, "inWorld", presence != null && presence.IsInWorldOrGenerating ? "true" : "false");
		putRaw(fields, escape, "gateWaiting", gate != null && gate.IsWaitingForReady ? "true" : "false");
		putText(fields, escape, "gateText", gate == null ? "" : gate.WaitingText);
		putRaw(fields, escape, "controls", quoteList(escape, controlIds(actions)));
		return finish(fields);
	}

	if (command == "open-window") {
		if (overlay == null || windowState == null) {
			return fail(escape, "no-online-ui", "the CUO Online UI is not reachable in this process");
		}
		if (!readBool(windowState, "Visible")) {
			var toggle = findMethodWith(overlay, "ToggleWindow", 1);
			if (toggle == null) { return fail(escape, "no-toggle", "OnlineUiOverlay.ToggleWindow was not found"); }
			if (session == null) { return fail(escape, "no-session", "SessionService is not available to carry the launcher's role"); }
			toggle.Invoke(overlay, new object[] { session.Role });
		}
		putRaw(fields, escape, "ok", "true");
		putText(fields, escape, "command", command);
		putRaw(fields, escape, "visible", readBool(windowState, "Visible") ? "true" : "false");
		putText(fields, escape, "page", pageName(windowState));
		return finish(fields);
	}

	if (command == "click" || command == "set-text") {
		if (overlay == null || actions == null) {
			return fail(escape, "no-action-table", "the Online UI window is not offering controls in this frame");
		}
		var offered = actions.Contains(argument);
		var applied = false;
		if (offered) {
			var apply = findMethodWith(overlay, "Apply", 1);
			if (apply == null) { return fail(escape, "no-apply", "OnlineUiOverlay.Apply was not found"); }
			CasualtiesUnknownOnline.Runtime.OnlineUi.OnlineUiIntent intent;
			if (command == "click") {
				intent = new CasualtiesUnknownOnline.Runtime.OnlineUi.OnlineUiIntent(
					CasualtiesUnknownOnline.Runtime.OnlineUi.OnlineUiIntentKind.ControlInvoked,
					argument);
			}
			else {
				intent = new CasualtiesUnknownOnline.Runtime.OnlineUi.OnlineUiIntent(
					CasualtiesUnknownOnline.Runtime.OnlineUi.OnlineUiIntentKind.ControlEdited,
					argument,
					text);
			}
			applied = (bool)apply.Invoke(overlay, new object[] { intent });
		}
		putRaw(fields, escape, "ok", "true");
		putText(fields, escape, "command", command);
		putText(fields, escape, "controlId", argument);
		putRaw(fields, escape, "offered", offered ? "true" : "false");
		putRaw(fields, escape, "applied", applied ? "true" : "false");
		putText(fields, escape, "page", pageName(windowState));
		putRaw(fields, escape, "controls", quoteList(escape, controlIds(actions)));
		return finish(fields);
	}

	if (command == "start-run") {
		System.Type preRunType = null;
		var loaded = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < loaded.Length && preRunType == null; i++) {
			preRunType = loaded[i].GetType("PreRunScript", false);
		}
		if (preRunType == null) {
			return fail(escape, "no-prerun-type", "the game's PreRunScript type is not loaded in this process");
		}
		object preRun = null;
		var instanceProperty = preRunType.GetProperty("instance", staticFlags);
		if (instanceProperty != null) { preRun = instanceProperty.GetValue(null, null); }
		if (preRun == null) {
			var instanceField = preRunType.GetField("instance", staticFlags);
			if (instanceField != null) { preRun = instanceField.GetValue(null); }
		}
		if (preRun == null) {
			return fail(escape, "no-prerun-instance", "the game's start screen is not loaded in this client yet");
		}
		var startRun = findMethodWith(preRun, "StartRun", 0);
		if (startRun == null) { return fail(escape, "no-start-run", "PreRunScript.StartRun was not found"); }
		try { startRun.Invoke(preRun, null); }
		catch (System.Exception exception) { return fail(escape, "start-run-failed", unwrap(exception)); }
		putRaw(fields, escape, "ok", "true");
		putText(fields, escape, "command", command);
		putRaw(fields, escape, "called", "true");
		putRaw(fields, escape, "inWorld", presence != null && presence.IsInWorldOrGenerating ? "true" : "false");
		putRaw(fields, escape, "gateWaiting", gate != null && gate.IsWaitingForReady ? "true" : "false");
		return finish(fields);
	}

	if (command == "quit") {
		putRaw(fields, escape, "ok", "true");
		putText(fields, escape, "command", command);
		putRaw(fields, escape, "quitting", "true");
		UnityEngine.Application.Quit();
		return finish(fields);
	}

	return fail(escape, "unknown-command", command);
})())
