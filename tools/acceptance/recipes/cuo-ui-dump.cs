// recipe: cuo-ui-dump
// args: none
// serves: online-ui-paired-controls-and-page-scroll (S1-S4)
// returns: ok, and one entry per RectTransform under the CUO Online UI window: its path, rect, active flag,
//          span (its box in the WINDOW's own units, left..right x bottom..top), LayoutElement, Image (sprite,
//          type, colour, raycast), TMP text, Button, Toggle, InputField and Slider — where a slider reports
//          the game's own background, filled fraction and handle with their rects, and an InputField its
//          text, focus and content type.
//
// It reads what the user judges on screen as numbers: a colour alpha of 0 is the hidden prefab art, a
// handle of 0x0 is a slider nobody can grab, and the page's own bar is the node named
// "CUO Online UI Scrollbar" with its active flag. Numbers are written with the invariant culture, so the
// answer is JSON a reader can parse on any machine.
//
// `span` is the reading the geometry of a page is JUDGED in: `rect` and `pos` are local to each node's own
// parent (a size and an anchored position), so a box's distance to the page's edge cannot be read off them
// alone (the paired row of ticket online-ui-paired-controls-and-page-scroll is exactly that: a row whose box
// should be the page's own width with the value box six units inside the page's right edge). The window's
// centre is 0,0 and the page inside it is inset by the shell's own padding, so at the window's DEFAULT
// 1000 x 700 the page's edges are -492.00 and 492.00 and a control six units inside the right one ends at
// 486.00 — but a canvas too small clamps the window, so read the page's own edges off the `Content` node's
// span rather than assuming those numbers.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var num = new System.Func<float, string>((value) => value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
	var all = UnityEngine.Object.FindObjectsOfType(typeof(UnityEngine.RectTransform), true);
	UnityEngine.Transform window = null;
	for (var i = 0; i < all.Length; i++) {
		var candidate = (UnityEngine.RectTransform)all[i];
		if (candidate.gameObject.name == "CUO Online UI Window") { window = candidate; break; }
	}
	if (window == null) { return "{\"ok\":false,\"error\":\"no CUO Online UI Window object\"}"; }

	var sb = new System.Text.StringBuilder();
	sb.Append("{\"ok\":true,\"nodes\":[");
	var corners = new UnityEngine.Vector3[4];
	var nodes = new System.Collections.Generic.List<UnityEngine.Transform>();
	var paths = new System.Collections.Generic.List<string>();
	nodes.Add(window);
	paths.Add("Window");
	var count = 0;
	while (nodes.Count > 0 && count < 320) {
		var t = nodes[0];
		var path = paths[0];
		nodes.RemoveAt(0);
		paths.RemoveAt(0);
		count++;
		if (count > 1) { sb.Append(","); }
		sb.Append("{\"path\":\"").Append(path.Replace("\"", "'")).Append("\"");
		sb.Append(",\"active\":").Append(t.gameObject.activeSelf ? "true" : "false");
		var rect = t as UnityEngine.RectTransform;
		if (rect != null) {
			sb.Append(",\"rect\":\"").Append(num(rect.rect.width)).Append("x").Append(num(rect.rect.height)).Append("\"");
			sb.Append(",\"pos\":\"").Append(num(rect.anchoredPosition.x)).Append(",").Append(num(rect.anchoredPosition.y)).Append("\"");
			sb.Append(",\"anchor\":\"").Append(num(rect.anchorMin.x)).Append(",").Append(num(rect.anchorMin.y))
				.Append("..").Append(num(rect.anchorMax.x)).Append(",").Append(num(rect.anchorMax.y))
				.Append(" pivot ").Append(num(rect.pivot.x)).Append(",").Append(num(rect.pivot.y)).Append("\"");
			rect.GetWorldCorners(corners);
			var bottomLeft = window.InverseTransformPoint(corners[0]);
			var topRight = window.InverseTransformPoint(corners[2]);
			sb.Append(",\"span\":\"").Append(num(bottomLeft.x)).Append("..").Append(num(topRight.x))
				.Append(" x ").Append(num(bottomLeft.y)).Append("..").Append(num(topRight.y)).Append("\"");
		}
		var le = t.GetComponent<UnityEngine.UI.LayoutElement>();
		if (le != null) {
			sb.Append(",\"le\":\"minW=").Append(num(le.minWidth)).Append(" prefW=").Append(num(le.preferredWidth)).Append(" flexW=").Append(num(le.flexibleWidth))
				.Append(" minH=").Append(num(le.minHeight)).Append(" prefH=").Append(num(le.preferredHeight)).Append(" flexH=").Append(num(le.flexibleHeight)).Append("\"");
		}
		var img = t.GetComponent<UnityEngine.UI.Image>();
		if (img != null) {
			sb.Append(",\"image\":{\"sprite\":\"").Append(img.sprite == null ? "(none)" : img.sprite.name)
				.Append("\",\"type\":\"").Append(img.type.ToString())
				.Append("\",\"color\":\"").Append(num(img.color.r)).Append(",").Append(num(img.color.g)).Append(",").Append(num(img.color.b)).Append(",").Append(num(img.color.a))
				.Append("\",\"ray\":").Append(img.raycastTarget ? "true" : "false").Append("}");
		}
		var txt = t.GetComponent<TMPro.TextMeshProUGUI>();
		if (txt != null) {
			var body = txt.text == null ? "" : txt.text;
			if (body.Length > 30) { body = body.Substring(0, 30); }
			sb.Append(",\"text\":\"").Append(body.Replace("\"", "'").Replace("\n", " ")).Append("\"");
			sb.Append(",\"fontSize\":").Append(num(txt.fontSize));
			sb.Append(",\"ray\":").Append(txt.raycastTarget ? "true" : "false");
		}
		var btn = t.GetComponent<UnityEngine.UI.Button>();
		if (btn != null) { sb.Append(",\"button\":true"); }
		var tg = t.GetComponent<UnityEngine.UI.Toggle>();
		if (tg != null) { sb.Append(",\"toggle\":").Append(tg.isOn ? "true" : "false"); }
		var input = t.GetComponent<TMPro.TMP_InputField>();
		if (input != null) {
			sb.Append(",\"input\":{\"text\":\"").Append((input.text == null ? "" : input.text).Replace("\"", "'"))
				.Append("\",\"type\":\"").Append(input.contentType.ToString())
				.Append("\",\"focused\":").Append(input.isFocused ? "true" : "false")
				.Append(",\"limit\":").Append(input.characterLimit.ToString(inv)).Append("}");
		}
		var sl = t.GetComponent<UnityEngine.UI.Slider>();
		if (sl != null) {
			var bg = sl.targetGraphic;
			var fill = sl.fillRect == null ? null : sl.fillRect.GetComponent<UnityEngine.UI.Graphic>();
			var handle = sl.handleRect == null ? null : sl.handleRect.GetComponent<UnityEngine.UI.Graphic>();
			sb.Append(",\"slider\":{\"value\":").Append(sl.value.ToString("F3", inv));
			sb.Append(",\"range\":\"").Append(num(sl.minValue)).Append("..").Append(num(sl.maxValue)).Append("\"");
			sb.Append(",\"bgSprite\":\"").Append(bg == null || ((UnityEngine.UI.Image)bg).sprite == null ? "(none)" : ((UnityEngine.UI.Image)bg).sprite.name).Append("\"");
			sb.Append(",\"bgAlpha\":").Append(bg == null ? "-1" : num(bg.color.a));
			sb.Append(",\"fillColor\":\"").Append(fill == null ? "(none)" : num(fill.color.r) + "," + num(fill.color.g) + "," + num(fill.color.b) + "," + num(fill.color.a)).Append("\"");
			sb.Append(",\"fillRect\":\"").Append(sl.fillRect == null ? "(none)" : num(sl.fillRect.rect.width) + "x" + num(sl.fillRect.rect.height)).Append("\"");
			sb.Append(",\"handleColor\":\"").Append(handle == null ? "(none)" : num(handle.color.r) + "," + num(handle.color.g) + "," + num(handle.color.b) + "," + num(handle.color.a)).Append("\"");
			sb.Append(",\"handleRect\":\"").Append(sl.handleRect == null ? "(none)" : num(sl.handleRect.rect.width) + "x" + num(sl.handleRect.rect.height)).Append("\"");
			sb.Append(",\"handleX\":\"").Append(sl.handleRect == null ? "(none)" : num(sl.handleRect.anchoredPosition.x)).Append("\"");
			sb.Append("}");
		}
		var scroll = t.GetComponent<UnityEngine.UI.ScrollRect>();
		if (scroll != null) {
			sb.Append(",\"scroll\":{\"vertical\":").Append(scroll.vertical ? "true" : "false");
			sb.Append(",\"position\":").Append(num(scroll.verticalNormalizedPosition));
			sb.Append(",\"content\":\"").Append(scroll.content == null ? "(none)" : num(scroll.content.rect.height)).Append("\"");
			sb.Append(",\"viewport\":\"").Append(scroll.viewport == null ? "(none)" : num(scroll.viewport.rect.height)).Append("\"");
			sb.Append("}");
		}
		sb.Append("}");
		for (var c = 0; c < t.childCount; c++) {
			nodes.Add(t.GetChild(c));
			paths.Add(path + "/" + t.GetChild(c).gameObject.name);
		}
	}
	sb.Append("],\"count\":").Append(count.ToString(inv)).Append("}");
	return sb.ToString();
}))()
