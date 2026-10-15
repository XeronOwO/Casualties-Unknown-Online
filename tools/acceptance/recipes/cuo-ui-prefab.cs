// recipe: cuo-ui-prefab
// args: none
// serves: online-ui-paired-controls-and-page-scroll
// returns: ok, and one entry per prefab the Online UI builds a control from — the ASSET's own geometry, read
//          through Resources.Load so that nothing CUO wrote to an instance can hide it: the root's rect, and
//          every node's relative path, active flag, rect, anchored position, sizeDelta, anchors and pivot,
//          plus the Slider component's own references (targetGraphic, fillRect, handleRect) with their
//          authored rects.
//
// It answers the question the ticket's own direction turns on — whether the game's own interior survives the
// compact height this page gives a control, and at what proportions it was authored — because a re-cut has to
// be derived from the prefab's own numbers, and an INSTANCE of the prefab no longer carries them: the row's
// layout group and CUO's own passes write the control's rects from the first frame on (ticket
// online-ui-paired-controls-and-page-scroll, the judging pass of 2026-10-15).
//
// The asset is read, never instantiated and never written, so this reading is the same on every run and on a
// page that was never opened. Numbers are written with the invariant culture, so the answer is JSON a reader
// can parse on any machine.
((System.Func<string>)(() => {
	var num = new System.Func<float, string>((value) => value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var paths = new string[] {
		"Special/GameSettingFloat",
		"Special/GameSettingInt",
		"Special/GameSettingBool",
		"Special/GameSettingDropdown",
		"Special/GameSettingLanguage"
	};
	var sb = new System.Text.StringBuilder();
	sb.Append("{\"ok\":true,\"prefabs\":[");
	var firstPrefab = true;
	var nodeCap = 64;
	for (var p = 0; p < paths.Length; p++) {
		if (!firstPrefab) { sb.Append(","); }
		firstPrefab = false;
		sb.Append("{\"path\":\"").Append(paths[p]).Append("\"");
		var prefab = UnityEngine.Resources.Load(paths[p], typeof(UnityEngine.GameObject)) as UnityEngine.GameObject;
		if (prefab == null) { sb.Append(",\"loaded\":false}"); continue; }
		sb.Append(",\"loaded\":true");
		var root = prefab.transform as UnityEngine.RectTransform;
		if (root != null) {
			sb.Append(",\"root\":\"").Append(num(root.rect.width)).Append("x").Append(num(root.rect.height)).Append("\"");
			sb.Append(",\"rootSize\":\"").Append(num(root.sizeDelta.x)).Append("x").Append(num(root.sizeDelta.y)).Append("\"");
		}
		sb.Append(",\"nodes\":[");
		var nodes = new System.Collections.Generic.List<UnityEngine.Transform>();
		var names = new System.Collections.Generic.List<string>();
		nodes.Add(prefab.transform);
		names.Add("");
		var written = 0;
		var truncated = false;
		while (nodes.Count > 0 && written < nodeCap) {
			var t = nodes[0];
			var path = names[0];
			nodes.RemoveAt(0);
			names.RemoveAt(0);
			if (written > 0) { sb.Append(","); }
			written++;
			sb.Append("{\"path\":\"").Append(path.Length == 0 ? "(root)" : path).Append("\"");
			sb.Append(",\"active\":").Append(t.gameObject.activeSelf ? "true" : "false");
			var rect = t as UnityEngine.RectTransform;
			if (rect != null) {
				sb.Append(",\"rect\":\"").Append(num(rect.rect.width)).Append("x").Append(num(rect.rect.height)).Append("\"");
				sb.Append(",\"size\":\"").Append(num(rect.sizeDelta.x)).Append("x").Append(num(rect.sizeDelta.y)).Append("\"");
				sb.Append(",\"pos\":\"").Append(num(rect.anchoredPosition.x)).Append(",").Append(num(rect.anchoredPosition.y)).Append("\"");
				sb.Append(",\"anchor\":\"").Append(num(rect.anchorMin.x)).Append(",").Append(num(rect.anchorMin.y))
					.Append("..").Append(num(rect.anchorMax.x)).Append(",").Append(num(rect.anchorMax.y))
					.Append(" pivot ").Append(num(rect.pivot.x)).Append(",").Append(num(rect.pivot.y)).Append("\"");
				sb.Append(",\"offset\":\"").Append(num(rect.offsetMin.x)).Append(",").Append(num(rect.offsetMin.y))
					.Append("..").Append(num(rect.offsetMax.x)).Append(",").Append(num(rect.offsetMax.y)).Append("\"");
			}
			var image = t.GetComponent(typeof(UnityEngine.UI.Image)) as UnityEngine.UI.Image;
			if (image != null) {
				sb.Append(",\"sprite\":\"").Append(image.sprite == null ? "(none)" : image.sprite.name)
					.Append("\",\"imageType\":\"").Append(image.type.ToString())
					.Append("\",\"color\":\"").Append(num(image.color.r)).Append(",").Append(num(image.color.g)).Append(",").Append(num(image.color.b)).Append(",").Append(num(image.color.a))
					.Append("\"");
			}
			var element = t.GetComponent(typeof(UnityEngine.UI.LayoutElement)) as UnityEngine.UI.LayoutElement;
			if (element != null) {
				sb.Append(",\"le\":\"minW=").Append(num(element.minWidth)).Append(" prefW=").Append(num(element.preferredWidth)).Append(" flexW=").Append(num(element.flexibleWidth))
					.Append(" minH=").Append(num(element.minHeight)).Append(" prefH=").Append(num(element.preferredHeight))
					.Append("\"");
			}
			var text = t.GetComponent(typeof(TMPro.TextMeshProUGUI)) as TMPro.TextMeshProUGUI;
			if (text != null) {
				var body = text.text == null ? "" : text.text;
				if (body.Length > 30) { body = body.Substring(0, 30); }
				sb.Append(",\"text\":\"").Append(body.Replace("\"", "'").Replace("\n", " ")).Append("\",\"fontSize\":").Append(num(text.fontSize));
			}
			sb.Append("}");
			for (var c = 0; c < t.childCount; c++) {
				nodes.Add(t.GetChild(c));
				names.Add(path.Length == 0 ? t.GetChild(c).gameObject.name : path + "/" + t.GetChild(c).gameObject.name);
			}
		}
		sb.Append("]");
		// A prefab with more nodes than the cap says so rather than quietly answering a shorter tree: a reader
		// who trusts a truncated list would judge the prefab's interior from half of it.
		truncated = nodes.Count > 0;
		sb.Append(",\"truncated\":").Append(truncated ? "true" : "false");
		sb.Append(",\"nodeCount\":").Append(written.ToString(inv));
		var slider = prefab.GetComponentInChildren(typeof(UnityEngine.UI.Slider), true) as UnityEngine.UI.Slider;
		if (slider != null) {
			sb.Append(",\"slider\":{");
			sb.Append("\"targetGraphic\":\"").Append(slider.targetGraphic == null ? "(none)" : slider.targetGraphic.name).Append("\"");
			sb.Append(",\"fillRect\":\"").Append(slider.fillRect == null ? "(none)" : num(slider.fillRect.rect.width) + "x" + num(slider.fillRect.rect.height)).Append("\"");
			sb.Append(",\"fillName\":\"").Append(slider.fillRect == null ? "(none)" : slider.fillRect.name).Append("\"");
			sb.Append(",\"handleRect\":\"").Append(slider.handleRect == null ? "(none)" : num(slider.handleRect.rect.width) + "x" + num(slider.handleRect.rect.height)).Append("\"");
			sb.Append(",\"handleName\":\"").Append(slider.handleRect == null ? "(none)" : slider.handleRect.name).Append("\"");
			sb.Append(",\"range\":\"").Append(num(slider.minValue)).Append("..").Append(num(slider.maxValue)).Append("\"");
			sb.Append(",\"direction\":\"").Append(slider.direction.ToString()).Append("\"");
			sb.Append("}");
		}
		sb.Append("}");
	}
	sb.Append("],\"prefabCount\":").Append(paths.Length.ToString(inv)).Append("}");
	return sb.ToString();
}))()
