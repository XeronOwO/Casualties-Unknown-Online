// recipe: panel-title
// args: target=n
// serves: the user's "面板里没有名字,灰框还在" report - the context menu draws a title bar with no text, and the question is whether the
//         MODEL carries a name and the label fails, or the model itself is empty. `open` sets the plugin menu controller's target and
//         position (the same fields the right-click sets) so a batch can read the panel WITHOUT an OS-level click; `read` reports the
//         title text the view was given, what the TMP label holds, and the label's rect, size, colour and the panel's width.
// returns: ok, modelTitle, labelText, labelWidth, labelHeight, labelFontSize, labelAlpha, labelEnabled, panelWidth, error, detail
//
// Reflection only, never a cast: the recipe's own assembly identities need not be the runtime's, and a failed cast reads as "absent".
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
	// One argument: target=0 reads the panel, any other value sets the menu's target (the in-process equivalent of the right-click).
	var target = (ulong)({{n:target}});
	var mode = target == 0 ? "read" : "open";

	// Plugin side: PluginHost._onlineUi is OnlineUiOverlay, whose _contextMenu is the plugin's menu controller.
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
	// Two hops: PluginHost._onlineUi → OnlineUiOverlay, whose own _onlineUi field is the object that owns the plugin menu controller.
	var innerField = overlay == null ? null : overlay.GetType().GetField("_onlineUi", Flags);
	var inner = innerField == null ? null : innerField.GetValue(overlay);
	var menuField = inner == null ? null : inner.GetType().GetField("_contextMenu", Flags);
	var menu = menuField == null ? null : menuField.GetValue(inner);
	if (menu == null) { return "{\"ok\":false,\"error\":\"no-menu-controller\"}"; }

	if (mode == "open") {
		var targetField = menu.GetType().GetField("_targetSteamId", Flags);
		var positionField = menu.GetType().GetField("_position", Flags);
		if (targetField == null || positionField == null) { return "{\"ok\":false,\"error\":\"no-menu-fields\"}"; }
		targetField.SetValue(menu, new System.Nullable<ulong>(target));
		positionField.SetValue(menu, new UnityEngine.Vector2(640f, 380f));
		return "{\"ok\":true,\"mode\":\"open\",\"target\":" + target.ToString(inv) + "}";
	}

	// Adapter side: the container's IOnlineUiSurface IS the GameAdapter, whose _onlineUiSurface holds the panel views.
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	var surface = services == null ? null : services.GetService(typeof(CasualtiesUnknownOnline.Runtime.GameAdapter.IOnlineUiSurface));
	if (surface == null) { return "{\"ok\":false,\"error\":\"no-surface-service\"}"; }
	var hostField = surface.GetType().GetField("_onlineUiSurface", Flags);
	var host = hostField == null ? null : hostField.GetValue(surface);
	var panelField = host == null ? null : host.GetType().GetField("_contextMenu", Flags);
	var panel = panelField == null ? null : panelField.GetValue(host);
	if (panel == null) { return "{\"ok\":false,\"error\":\"no-panel-view\"}"; }
	var titleTextField = panel.GetType().GetField("_titleText", Flags);
	var modelTitle = titleTextField == null ? "" : (titleTextField.GetValue(panel) as string);
	var titleField = panel.GetType().GetField("_title", Flags);
	var title = titleField == null ? null : titleField.GetValue(panel);
	if (title == null) { return "{\"ok\":false,\"error\":\"no-title-label\",\"modelTitle\":\"" + (modelTitle ?? "") + "\"}"; }
	var titleText = title.GetType().GetProperty("text", Flags);
	var fontSize = title.GetType().GetProperty("fontSize", Flags);
	var colorProperty = title.GetType().GetProperty("color", Flags);
	var rectProperty = title.GetType().GetProperty("rectTransform", Flags);
	var enabledProperty = title.GetType().GetProperty("enabled", Flags);
	var labelRect = rectProperty == null ? null : rectProperty.GetValue(title, null) as UnityEngine.RectTransform;
	var color = colorProperty == null ? default(UnityEngine.Color) : (UnityEngine.Color)colorProperty.GetValue(title, null);
	// The link that actually decides whether glyphs reach the screen: the label needs an ACTIVE hierarchy, a font asset, an unculled
	// canvas renderer and a built mesh. Reading the text alone said "all good" while the frame showed an empty bar.
	var activeProperty = title.GetType().GetProperty("isActiveAndEnabled", Flags);
	var fontProperty = title.GetType().GetProperty("font", Flags);
	var meshProperty = title.GetType().GetProperty("textInfo", Flags);
	var meshInfo = meshProperty == null ? null : meshProperty.GetValue(title, null);
	var meshCountProperty = meshInfo == null ? null : meshInfo.GetType().GetProperty("characterCount", Flags);
	var canvasRendererProperty = title.GetType().GetProperty("canvasRenderer", Flags);
	var canvasRenderer = canvasRendererProperty == null ? null : canvasRendererProperty.GetValue(title, null);
	var cullProperty = canvasRenderer == null ? null : canvasRenderer.GetType().GetProperty("cull", Flags);
	var rectField = panel.GetType().GetField("_rect", Flags);
	var panelRect = rectField == null ? null : rectField.GetValue(panel) as UnityEngine.RectTransform;
	// How many menus are actually in the scene: the frame shows a ~195px menu while this view reports 289, so if two objects carry the
	// menu's name the drawn one is not the one the surface applies models to.
	var menus = "";
	var found = UnityEngine.Object.FindObjectsOfType(typeof(UnityEngine.RectTransform));
	for (var i = 0; i < found.Length; i++) {
		var rect = found[i] as UnityEngine.RectTransform;
		if (rect == null || rect.name != "CUO Online UI Player Menu") { continue; }
		menus += rect.sizeDelta.x.ToString("0.##", inv) + "/" + (rect.gameObject.activeInHierarchy ? "a" : "i") + " ";
	}
	var panelCount = 0;
	for (var i = 0; i < found.Length; i++) {
		var rect = found[i] as UnityEngine.RectTransform;
		if (rect != null && rect.name == "CUO Online UI Player Menu") { panelCount++; }
	}
	// TMP_TextInfo.characterCount is a FIELD, not a property: reading it as one answered -1 and told the batch nothing.
	var characters = -2;
	if (meshInfo != null) {
		var countField = meshInfo.GetType().GetField("characterCount", Flags);
		if (countField != null) { characters = (int)countField.GetValue(meshInfo); }
	}
	var materialName = "<none>";
	var materialProperty = title.GetType().GetProperty("fontSharedMaterial", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
	var material = materialProperty == null ? null : materialProperty.GetValue(title, null) as UnityEngine.Material;
	if (material != null) { materialName = material.name; }
	var fontName = "<none>";
	var fontValue = fontProperty == null ? null : fontProperty.GetValue(title, null);
	if (fontValue != null) { fontName = ((UnityEngine.Object)fontValue).name; }
	var wrap = title.GetType().GetProperty("enableWordWrapping", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
	var overflow = title.GetType().GetProperty("overflowMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
	// If the label holds text but generated no glyphs, ask TMP to reparse once and read again: that separates "the mesh was never built" from
	// "the font cannot draw these characters", which are different fixes.
	var charactersAfter = characters;
	if (characters == 0) {
		var force = title.GetType().GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) });
		if (force != null) {
			force.Invoke(title, new object[] { true, true });
			if (meshInfo != null) {
				var countField = meshInfo.GetType().GetField("characterCount", Flags);
				if (countField != null) { charactersAfter = (int)countField.GetValue(meshInfo); }
			}
		}
	}
	// CONTROL: the same two reads on a label that DOES render (a row's own text). If it also reports zero characters the instrument is lying
	// and the title's zero means nothing; if it reports the row's characters, the title really is unparsed.
	var controlText = "";
	var controlCharacters = -3;
	var all = UnityEngine.Object.FindObjectsOfType(typeof(UnityEngine.RectTransform));
	for (var i = 0; i < all.Length; i++) {
		var candidate = all[i] as UnityEngine.RectTransform;
		if (candidate == null || candidate.name == "Title" || candidate.IsChildOf(panelRect) == false) { continue; }
		var candidateText = candidate.GetComponent("TextMeshProUGUI");
		if (candidateText == null) { continue; }
		var candidateTextProperty = candidateText.GetType().GetProperty("text", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
		var candidateInfoProperty = candidateText.GetType().GetProperty("textInfo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
		controlText = (candidateTextProperty == null ? "" : (candidateTextProperty.GetValue(candidateText, null) as string)) ?? "";
		if (controlText.Length == 0) { continue; }
		var info = candidateInfoProperty == null ? null : candidateInfoProperty.GetValue(candidateText, null);
		var countField = info == null ? null : info.GetType().GetField("characterCount", Flags);
		controlCharacters = countField == null ? -4 : (int)countField.GetValue(info);
		break;
	}
	// BISECT the string: the same component, four strings, a forced reparse after each. A count that moves with the string proves the parser
	// works and the name's own characters are what it cannot take; a count that stays 0 proves the component never parses at all.
	var titleTextProperty = title.GetType().GetProperty("text", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
	var forceMethod = title.GetType().GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) });
	var probeCounts = "";
	if (titleTextProperty != null && forceMethod != null) {
		var probes = new[] { "Test123", "Kaguya", "small", "x" };
		for (var i = 0; i < probes.Length; i++) {
			titleTextProperty.SetValue(title, probes[i], null);
			forceMethod.Invoke(title, new object[] { true, true });
			var info = meshInfo;
			var countField = info == null ? null : info.GetType().GetField("characterCount", Flags);
			probeCounts += (countField == null ? "-5" : ((int)countField.GetValue(info)).ToString(inv)) + ",";
		}
	}
	// WHAT ELSE is on the label object: a component of the game's own that rewrites the text every frame would explain a count of zero for
	// every string while a row label parses normally.
	var components = "";
	var parts = ((UnityEngine.Component)title).gameObject.GetComponents(typeof(UnityEngine.Component));
	for (var i = 0; i < parts.Length; i++) { components += parts[i].GetType().Name + " "; }
	return "{\"ok\":true"
		+ ",\"labelComponents\":\"" + components + "\""
		+ ",\"probeCounts\":\"" + probeCounts + "\""
		+ ",\"controlText\":\"" + controlText + "\""
		+ ",\"controlCharacters\":" + controlCharacters.ToString(inv)
		+ ",\"charactersAfter\":" + charactersAfter.ToString(inv)
		+ ",\"modelTitle\":\"" + (modelTitle ?? "") + "\""
		+ ",\"labelText\":\"" + ((titleText == null ? "" : (titleText.GetValue(title, null) as string)) ?? "") + "\""
		+ ",\"labelWidth\":" + (labelRect == null ? "0" : labelRect.rect.width.ToString("0.##", inv))
		+ ",\"labelHeight\":" + (labelRect == null ? "0" : labelRect.rect.height.ToString("0.##", inv))
		+ ",\"labelFontSize\":" + (fontSize == null ? "0" : ((float)fontSize.GetValue(title, null)).ToString("0.##", inv))
		+ ",\"labelAlpha\":" + color.a.ToString("0.###", inv)
		+ ",\"labelColor\":\"" + color.r.ToString("0.##", inv) + "," + color.g.ToString("0.##", inv) + "," + color.b.ToString("0.##", inv) + "\""
		+ ",\"labelPos\":\"" + (labelRect == null ? "" : labelRect.anchoredPosition.x.ToString("0.##", inv) + "," + labelRect.anchoredPosition.y.ToString("0.##", inv)) + "\""
		+ ",\"parent\":\"" + (labelRect == null || labelRect.parent == null ? "?" : labelRect.parent.name) + "\""
		+ ",\"labelEnabled\":" + (enabledProperty != null && (bool)enabledProperty.GetValue(title, null) ? "true" : "false")
		+ ",\"labelActive\":" + (activeProperty != null && (bool)activeProperty.GetValue(title, null) ? "true" : "false")
		+ ",\"font\":" + (fontValue == null ? "\"<null>\"" : "\"" + fontName + "\"")
		+ ",\"material\":\"" + materialName + "\""
		+ ",\"characters\":" + characters.ToString(inv)
		+ ",\"wordWrap\":" + (wrap == null ? "?" : ((bool)wrap.GetValue(title, null) ? "true" : "false"))
		+ ",\"overflow\":\"" + (overflow == null ? "?" : overflow.GetValue(title, null).ToString()) + "\""
		+ ",\"culled\":" + (cullProperty == null ? "?" : ((bool)cullProperty.GetValue(canvasRenderer, null) ? "true" : "false"))
		+ ",\"panelWidth\":" + (panelRect == null ? "0" : panelRect.sizeDelta.x.ToString("0.##", inv))
		+ ",\"menus\":" + panelCount.ToString(inv)
		+ ",\"menuWidths\":\"" + menus + "\""
		+ "}";
}))()
