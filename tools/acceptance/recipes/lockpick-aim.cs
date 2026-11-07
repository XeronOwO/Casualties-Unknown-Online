// recipe: lockpick-aim
// args: none
// serves: save-mid-run-consistent-cut row 3 (a save made with open lockables)
// returns: ok, kind, correctAngle, anglePrecision, pickLevel, targetX, targetY, screenX, screenY, baseX, baseY, uiScale, health, handX, handY, handMagnitude, handAngle, error
//
// Computes the virtual pointer's screen point that puts the lockpick minigame's hand at the CORRECT angle and at a
// distance inside the game's own clicking band. The game's `MinigameBase.GetMousePos` maps the pointer to handPos as
// `(mousePosition - minigameScreen.transform.position) / PlayerCamera.uiScale`, and the lockpick's `HandAngle` /
// `HandDistance` read `handPos` (atan2 degrees, magnitude); the game's Update only sets `clickingInside` on the frame
// the attack bind goes down, and its PhysicsUpdate then freezes handPos — so the batch moves the pointer here, waits
// for the hand to settle, presses the bind, and the lock turns to `toDestroy.health = 0`. Every game field is read by
// reflection because the evaluator cannot name the game's private minigame type; the distance 220 is the midpoint of
// the game's own (195, 247) band. Read-only: it stages nothing.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var main = MinigameBase.main;
	if (main == null) { return "{\"ok\":false,\"error\":\"no-minigame-base\"}"; }
	var current = main.currentMinigame;
	if (current == null) { return "{\"ok\":false,\"error\":\"no-minigame\"}"; }
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var type = current.GetType();
	var correctField = type.GetField("correctAngle", flags);
	if (correctField == null) { return "{\"ok\":false,\"error\":\"not-lockpick\",\"kind\":\"" + type.Name + "\"}"; }
	var angle = System.Convert.ToSingle(correctField.GetValue(current));

	var screenField = typeof(MinigameBase).GetField("minigameScreen", flags);
	if (screenField == null) { return "{\"ok\":false,\"error\":\"no-minigame-screen\"}"; }
	var screenRect = screenField.GetValue(main) as UnityEngine.RectTransform;
	if (screenRect == null) { return "{\"ok\":false,\"error\":\"no-screen-transform\"}"; }

	var rad = angle * UnityEngine.Mathf.Deg2Rad;
	var target = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(rad), UnityEngine.Mathf.Sin(rad)) * 220f;
	var scale = PlayerCamera.uiScale;
	var basePos = (UnityEngine.Vector2)screenRect.transform.position;
	var screen = basePos + target * scale;
	var hand = main.handPos;
	var handAngle = UnityEngine.Mathf.Atan2(hand.y, hand.x) * UnityEngine.Mathf.Rad2Deg;
	if (handAngle < 0f) { handAngle += 360f; }

	var health = -1f;
	var destroyField = type.GetField("toDestroy", flags);
	if (destroyField != null) {
		var targetObj = destroyField.GetValue(current);
		if (targetObj != null) {
			var healthField = targetObj.GetType().GetField("health", flags);
			if (healthField != null) { health = System.Convert.ToSingle(healthField.GetValue(targetObj)); }
		}
	}

	return "{\"ok\":true,\"kind\":\"" + type.Name + "\""
		+ ",\"correctAngle\":" + angle.ToString("0.###", inv)
		+ ",\"anglePrecision\":" + System.Convert.ToSingle(type.GetField("anglePrecision", flags).GetValue(current)).ToString("0.###", inv)
		+ ",\"pickLevel\":" + System.Convert.ToInt32(type.GetField("pickLevel", flags).GetValue(current)).ToString(inv)
		+ ",\"targetX\":" + target.x.ToString("0.###", inv)
		+ ",\"targetY\":" + target.y.ToString("0.###", inv)
		+ ",\"screenX\":" + screen.x.ToString("0.###", inv)
		+ ",\"screenY\":" + screen.y.ToString("0.###", inv)
		+ ",\"baseX\":" + basePos.x.ToString("0.###", inv)
		+ ",\"baseY\":" + basePos.y.ToString("0.###", inv)
		+ ",\"uiScale\":" + scale.ToString("0.###", inv)
		+ ",\"health\":" + health.ToString("0.###", inv)
		+ ",\"handX\":" + hand.x.ToString("0.###", inv)
		+ ",\"handY\":" + hand.y.ToString("0.###", inv)
		+ ",\"handMagnitude\":" + hand.magnitude.ToString("0.###", inv)
		+ ",\"handAngle\":" + handAngle.ToString("0.###", inv)
		+ "}";
}))()
