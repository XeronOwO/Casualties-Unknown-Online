// recipe: minigame-live
// args: none
// returns: ok, hasMain, kind, handClicking, handStarted, handMagnitude, angle, didWrap, lastActionAngle, fill, clickingInside, lockProgress, correctAngle, anglePrecision, error, detail
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

	// The lockpick minigame's own fields: the two it decides the stuck branch on, plus its progress and this run's correct angle. They are declared here rather
	// than inferred because the stuck accumulation lives INSIDE `if (clickingInside)`, so a press that misses the ring looks exactly like a press that never
	// registered (measured 2026-11-01: handClicking=true and handMagnitude inside 195..247, yet nothing played).
	var inside = current.GetType().GetField("clickingInside", flags);
	var progress = current.GetType().GetField("lockProgress", flags);
	var correct = current.GetType().GetField("correctAngle", flags);
	var precision = current.GetType().GetField("anglePrecision", flags);
	if (inside != null) { body += ",\"clickingInside\":" + (System.Convert.ToBoolean(inside.GetValue(current)) ? "true" : "false"); }
	if (progress != null) { body += ",\"lockProgress\":" + System.Convert.ToSingle(progress.GetValue(current)).ToString("0.###", inv); }
	if (correct != null) { body += ",\"correctAngle\":" + System.Convert.ToSingle(correct.GetValue(current)).ToString("0.#", inv); }
	if (precision != null) { body += ",\"anglePrecision\":" + System.Convert.ToSingle(precision.GetValue(current)).ToString("0.###", inv); }

	// The amputation minigame's own progress: it ends at `cutProgress >= 1` (AmputationMinigame.cs:73-75) and accumulates from the hand's
	// frame-to-frame velocity, so a shake that never ends has to be told apart from one that is merely slow (measured 2026-11-02).
	var cut = current.GetType().GetField("cutProgress", flags);
	if (cut != null) { body += ",\"cutProgress\":" + System.Convert.ToSingle(cut.GetValue(current)).ToString("0.####", inv); }

	// The amputation minigame's cutting band: `cutting` is `handPos.y <= maxHandPos + 10f` (AmputationMinigame.cs:46-50), and
	// `PhysicsUpdate` clamps the hand to at least `maxHandPos`, so the hand has to sit INSIDE a ten-unit band for the cut to advance.
	var maxHand = current.GetType().GetField("maxHandPos", flags);
	if (maxHand != null) { body += ",\"maxHandPos\":" + System.Convert.ToSingle(maxHand.GetValue(current)).ToString("0.###", inv); }
	body += ",\"handPosY\":" + main.handPos.y.ToString("0.###", inv);

	return body + "}";
}))()
