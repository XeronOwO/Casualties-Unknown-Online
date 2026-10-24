// recipe: trade-open
// args: mode=s
// returns: ok, mode, traderCount, hostile, buildHealth, currentTrader, menuActive, traderX, traderY, error, detail
//
// Opens the game's own TRADE MENU through its production path: the nearest `TraderScript` sets itself as
// `PlayerCamera.main.currentTrader` (its own interaction does that when the trader is not hostile and its build health is above
// 200), then `TraderScript.OnUse()` toggles the menu — the exact call the player's interact makes. `mode=close` toggles it shut.
// A setup substitution, not a player entry: the player's own entry is walking up and interacting.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type traderType = null;
	System.Type cameraType = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (traderType == null) { try { traderType = assemblies[i].GetType("TraderScript", false); } catch { traderType = null; } }
		if (cameraType == null) { try { cameraType = assemblies[i].GetType("PlayerCamera", false); } catch { cameraType = null; } }
	}
	if (traderType == null) { return "{\"ok\":false,\"error\":\"no-trader-type\"}"; }
	if (cameraType == null) { return "{\"ok\":false,\"error\":\"no-camera-type\"}"; }
	var camera = UnityEngine.Object.FindObjectOfType(cameraType);
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-player-camera\"}"; }
	var traders = UnityEngine.Object.FindObjectsOfType(traderType);
	if (traders == null || traders.Length == 0) { return "{\"ok\":false,\"error\":\"no-trader\"}"; }
	var currentField = cameraType.GetField("currentTrader", flags);
	var lastField = cameraType.GetField("lastTrader", flags);
	var menuField = cameraType.GetField("tradeMenu", flags);
	var menuActive = false;
	if (menuField != null) {
		var menu = menuField.GetValue(camera) as UnityEngine.GameObject;
		if (menu != null) { menuActive = menu.activeSelf; }
	}
	if (mode == "close") {
		var onUseClose = traderType.GetMethod("OnUse", flags);
		if (onUseClose != null) { onUseClose.Invoke(traders[0], null); }
		return "{\"ok\":true,\"mode\":\"close\",\"menuActive\":" + (menuActive ? "true" : "false") + "}";
	}
	// Nearest non-hostile trader with a healthy build: the game's own precondition for offering the trade.
	var best = traders[0];
	System.Type bodyType = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (bodyType == null) { try { bodyType = assemblies[i].GetType("Body", false); } catch { bodyType = null; } }
	}
	var body = bodyType != null ? UnityEngine.Object.FindObjectOfType(bodyType) as UnityEngine.Component : null;
	var bestDistance = float.MaxValue;
	var hostileField = traderType.GetField("hostile", flags);
	for (var i = 0; i < traders.Length; i++) {
		var hostile = hostileField != null && (bool)hostileField.GetValue(traders[i]);
		if (hostile) { continue; }
		var component = (UnityEngine.Component)traders[i];
		var distance = body != null ? UnityEngine.Vector3.Distance(body.transform.position, component.transform.position) : i;
		if (distance < bestDistance) { bestDistance = distance; best = traders[i]; }
	}
	var bestComponent = (UnityEngine.Component)best;
	if (currentField != null) { currentField.SetValue(camera, best); }
	if (lastField != null) { lastField.SetValue(camera, best); }
	var onUse = traderType.GetMethod("OnUse", flags);
	if (onUse == null) { return "{\"ok\":false,\"error\":\"no-onuse\"}"; }
	onUse.Invoke(best, null);
	var active = false;
	if (menuField != null) {
		var menuAfter = menuField.GetValue(camera) as UnityEngine.GameObject;
		if (menuAfter != null) { active = menuAfter.activeSelf; }
	}
	var health = 0f;
	var buildField = traderType.GetField("build", flags);
	if (buildField != null) {
		var build = buildField.GetValue(best);
		if (build != null) {
			var healthField = build.GetType().GetField("health", flags);
			if (healthField != null) { health = (float)healthField.GetValue(build); }
		}
	}
	return "{\"ok\":true,\"mode\":\"open\",\"traderCount\":" + traders.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"buildHealth\":" + health.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"currentTrader\":" + (currentField != null && currentField.GetValue(camera) != null ? "true" : "false")
		+ ",\"menuActive\":" + (active ? "true" : "false")
		+ ",\"traderX\":" + bestComponent.transform.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"traderY\":" + bestComponent.transform.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
