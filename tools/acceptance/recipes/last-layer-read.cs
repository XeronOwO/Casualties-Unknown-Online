// recipe: last-layer-read
// args: none
// returns: ok, layer, unchipped, blockItemUsage, endLab, doingEscape, bodyX, bodyY, attackBind, attackBindCode, handSlot, handItem, woundView, pointerOverUI, minigame, liquid, conscious, allowUseItem, attackCooldown, attackRot, error
//
// Reads each client's own last-layer gate: the native layer index, the run's
// unchipped switch, the local PlayerCamera gate, the local EndLabScript state,
// and the local body's native attack fields. It never reads the peer.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	var end = EndLabScript.instance;
	var cooldown = body == null ? -1f : body.attackCooldown;
	var rot = body == null ? 0f : body.attackRot;
	var attackBind = KeyBinds.GetBind("attack");
	var hand = body == null ? -1 : body.handSlot;
	var item = body == null ? null : body.GetItem(body.handSlot);
	var minigame = MinigameBase.main != null && MinigameBase.main.currentMinigame != null;
	var liquid = LiquidTransfer.instance != null;
	return "{\"ok\":true,\"layer\":" + world.biomeDepth
		+ ",\"unchipped\":" + (WorldGeneration.unchipped ? "true" : "false")
		+ ",\"blockItemUsage\":" + (camera.blockItemUsage ? "true" : "false")
		+ ",\"endLab\":" + (end != null ? "true" : "false")
		+ ",\"doingEscape\":" + (end != null && end.doingEscape ? "true" : "false")
		+ ",\"bodyX\":" + (body == null ? 0f : body.transform.position.x).ToString("0.###", inv)
		+ ",\"bodyY\":" + (body == null ? 0f : body.transform.position.y).ToString("0.###", inv)
		+ ",\"generating\":" + (world.generatingWorld ? "true" : "false")
		+ ",\"paused\":" + (PauseHandler.main != null && PauseHandler.main.isPaused ? "true" : "false")
		+ ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", inv)
		+ ",\"attackBind\":\"" + attackBind + "\""
		+ ",\"attackBindCode\":" + ((int)attackBind)
		+ ",\"handSlot\":" + hand
		+ ",\"handItem\":\"" + (item != null ? item.name : "none") + "\""
		+ ",\"woundView\":" + (camera.woundView != null && camera.woundView.activeSelf ? "true" : "false")
		+ ",\"pointerOverUI\":" + (UIUtil.IsPointerOverUIElement() ? "true" : "false")
		+ ",\"minigame\":" + (minigame ? "true" : "false")
		+ ",\"liquid\":" + (liquid ? "true" : "false")
		+ ",\"conscious\":" + (body != null && body.conscious ? "true" : "false")
		+ ",\"allowUseItem\":" + (body != null && body.allowUseItem ? "true" : "false")
		+ ",\"forceNoMovement\":" + (body != null && body.forceNoMovement ? "true" : "false")
		+ ",\"forceWalk\":" + (body != null && body.forceWalk ? "true" : "false")
		+ ",\"attackCooldown\":" + cooldown.ToString("0.###", inv)
		+ ",\"attackRot\":" + rot.ToString("0.###", inv) + "}";
}))()
