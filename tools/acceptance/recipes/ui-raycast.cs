// recipe: ui-raycast
// args: none
// returns: ok, pointerX, pointerY, overUi, count, hits (name, layer, blocks)
//
// Answers the question `PlayerCamera.HandleAttacks` asks before it can start a swing: is the pointer over a UI
// element? (`PlayerCamera.cs:1997` -> `UIUtil.IsPointerOverUIElement()` -> an EventSystem raycast at
// `Input.mousePosition`, `UIUtil.cs`). A row that presses the attack key and sees nothing happen needs to know
// which element eats the pointer, not just that the swing never came out (measured 2026-11-22: 803 held answers,
// 805 attack-path entries, no damage).
((System.Func<string>)(() => {
	var pointer = Input.mousePosition;
	var eventSystem = UnityEngine.EventSystems.EventSystem.current;
	if (eventSystem == null) { return "{\"ok\":false,\"error\":\"no-event-system\"}"; }
	var data = new UnityEngine.EventSystems.PointerEventData(eventSystem);
	data.position = new Vector2(pointer.x, pointer.y);
	var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
	eventSystem.RaycastAll(data, results);
	var uiLayer = UnityEngine.LayerMask.NameToLayer("UI");
	var over = false;
	var hits = new System.Collections.Generic.List<string>();
	var quote = (System.Func<string, string>)(text => (text ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\""));
	for (var i = 0; i < results.Count && i < 8; i++) {
		var go = results[i].gameObject;
		if (go == null) { continue; }
		var blocks = go.layer == uiLayer;
		if (blocks) { over = true; }
		hits.Add("{\"name\":\"" + quote(go.name) + "\",\"layer\":" + go.layer + ",\"blocks\":" + (blocks ? "true" : "false") + "}");
	}
	return "{\"ok\":true,\"pointerX\":" + System.Convert.ToInt32(pointer.x) + ",\"pointerY\":" + System.Convert.ToInt32(pointer.y)
		+ ",\"overUi\":" + (over ? "true" : "false") + ",\"count\":" + results.Count + ",\"hits\":[" + string.Join(",", hits) + "]}";
}))()
