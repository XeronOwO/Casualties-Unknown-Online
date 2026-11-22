// recipe: lockpick-align
// args: radius=n
// serves: 1.0-rebuild-each-acceptance-row-from-the-game (row 4, lockpick)
// returns: ok, kind, correctAngle, handAngle, error, precision, handDistance, max, screenX, screenY
//
// Stages the pointer on the lock's correct angle at the given radius and reports where the hand actually ended up.
// The game counts the turn only while the hand is inside 195..247 units and within `anglePrecision` of
// `correctAngle`, because `MaxTurnProgress()` is `clamp01(1 - |HandAngle - correctAngle| / 90)`
// (`LockpingMinigame.cs:106-135`); the hand is a damped body (`MinigameBase.UpdateHandPhysics`), so one staging
// call is not enough and the row calls this again until the error is small.
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
	var precisionField = type.GetField("anglePrecision", flags);
	var correct = System.Convert.ToSingle(correctField.GetValue(current), inv);
	var precision = precisionField == null ? 0f : System.Convert.ToSingle(precisionField.GetValue(current), inv);

	var screenField = typeof(MinigameBase).GetField("minigameScreen", flags);
	var screenRect = screenField == null ? null : screenField.GetValue(main) as UnityEngine.RectTransform;
	if (screenRect == null) { return "{\"ok\":false,\"error\":\"no-screen-transform\"}"; }

	var rad = correct * UnityEngine.Mathf.Deg2Rad;
	var radius = {{n:radius}};
	var target = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(rad), UnityEngine.Mathf.Sin(rad)) * radius;
	var scale = PlayerCamera.uiScale;
	var basePos = (UnityEngine.Vector2)screenRect.transform.position;
	var screen = new UnityEngine.Vector2(basePos.x + target.x * scale, basePos.y + target.y * scale);

	System.Type vi = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && vi == null; i++) {
		System.Type[] types;
		try { types = assemblies[i].GetTypes(); } catch { continue; }
		for (var t = 0; t < types.Length; t++) { if (types[t].Name == "VirtualInput") { vi = types[t]; break; } }
	}
	if (vi == null) { return "{\"ok\":false,\"error\":\"no-VirtualInput\"}"; }
	const System.Reflection.BindingFlags Statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	vi.GetMethod("MoveTo", Statics).Invoke(null, new object[] { screen.x, screen.y });

	var hand = main.handPos;
	var angle = UnityEngine.Mathf.Atan2(hand.y, hand.x) * UnityEngine.Mathf.Rad2Deg;
	if (angle < 0f) { angle += 360f; }
	var error = correct - angle;
	if (error > 180f) { error -= 360f; }
	if (error < -180f) { error += 360f; }

	return "{\"ok\":true,\"kind\":\"" + type.Name + "\""
		+ ",\"correctAngle\":" + correct.ToString("0.###", inv)
		+ ",\"handAngle\":" + angle.ToString("0.###", inv)
		+ ",\"error\":" + error.ToString("0.###", inv)
		+ ",\"precision\":" + precision.ToString("0.###", inv)
		+ ",\"handDistance\":" + hand.magnitude.ToString("0.###", inv)
		+ ",\"screenX\":" + screen.x.ToString("0.###", inv)
		+ ",\"screenY\":" + screen.y.ToString("0.###", inv) + "}";
}))()
