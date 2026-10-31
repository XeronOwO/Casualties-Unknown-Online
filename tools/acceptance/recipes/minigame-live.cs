// recipe: minigame-live
// args: none
// returns: ok, hasMain, kind, handClicking, handStarted, handMagnitude, angle, didWrap, lastActionAngle, fill, error, detail
//
// Reads the LOCAL minigame's own state as the game computes it — the fields `MinigameBase.UpdateHandVisuals` writes from `Input`
// and the bandage minigame's own progress fields. It exists because a driver that writes those fields is overwritten every frame
// (they come from `Input.GetKey`/`GetMouseButton` through the bind), so the only honest question is what the game ITSELF saw:
// was the attack bind held, how far has the ring turned, and how many of the game's own actions have run.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var main = MinigameBase.main;
	if (main == null) { return "{\"ok\":true,\"hasMain\":false}"; }

	var current = main.currentMinigame;
	if (current == null) { return "{\"ok\":true,\"hasMain\":true,\"kind\":\"none\",\"handClicking\":" + (main.handClicking ? "true" : "false") + "}"; }

	var kind = current.GetType().Name;
	var clicking = main.handClicking;
	var started = main.handStartedClicking;
	var magnitude = main.handPos.magnitude;
	var body = "{\"ok\":true,\"hasMain\":true,\"kind\":\"" + kind + "\""
		+ ",\"handClicking\":" + (clicking ? "true" : "false")
		+ ",\"handStarted\":" + (started ? "true" : "false")
		+ ",\"handMagnitude\":" + magnitude.ToString("0.###", inv);

	// The bandage minigame's own fields: the ring angle it tracks, whether it wrapped, the action counter, and the sprite's fill.
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var angle = current.GetType().GetField("bandageAngle", flags);
	var didWrap = current.GetType().GetField("didWrap", flags);
	var lastAction = current.GetType().GetField("lastActionAngle", flags);
	var sprite = current.GetType().GetField("bandageSprite", flags);
	if (angle != null) { body += ",\"angle\":" + System.Convert.ToSingle(angle.GetValue(current)).ToString("0.###", inv); }
	if (didWrap != null) { body += ",\"didWrap\":" + (System.Convert.ToBoolean(didWrap.GetValue(current)) ? "true" : "false"); }
	if (lastAction != null) { body += ",\"lastActionAngle\":" + System.Convert.ToInt32(lastAction.GetValue(current)).ToString(inv); }
	if (sprite != null) {
		var image = sprite.GetValue(current) as UnityEngine.UI.Image;
		if (image != null) { body += ",\"fill\":" + image.fillAmount.ToString("0.###", inv); }
	}

	return body + "}";
}))()
