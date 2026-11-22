// recipe: lockpick-enter
// args: press=n radius=n
// serves: 1.0-rebuild-each-acceptance-row-from-the-game (row 4, lockpick)
// returns: ok, kind, correctAngle, uiScale, baseX, baseY, screenX, screenY, handX, handY, handDistance, handAngle, pressed
//
// Puts the hand inside the ring and presses IN THE SAME EVAL. The game builds the minigame on the
// down edge of the interaction click and reads `clickingInside` only on the frame the attack bind
// goes down (`LockpingMinigame.cs:114-118`), so a move and a press in two round trips leave frames
// in between: measured 2026-11-23, `lockpick-aim` answered `no-minigame` right after the row's
// press-then-release had started it (`lock-25`), because the release ended the drag and the hand
// fell back to the real pointer.
//
// The screen point is computed with the game's own map (`handPos = (mouse - minigameScreen) / uiScale`,
// `lockpick-aim`'s measured relation); 220 is the midpoint of the game's own (195, 247) band.
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
	var target = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(rad), UnityEngine.Mathf.Sin(rad)) * {{n:radius}};
	var scale = PlayerCamera.uiScale;
	var basePos = (UnityEngine.Vector2)screenRect.transform.position;
	var screen = new UnityEngine.Vector2(basePos.x + target.x * scale, basePos.y + target.y * scale);

	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type vi = null;
	for (var i = 0; i < assemblies.Length && vi == null; i++) {
		System.Type[] types;
		try { types = assemblies[i].GetTypes(); } catch { continue; }
		for (var t = 0; t < types.Length; t++) { if (types[t].Name == "VirtualInput") { vi = types[t]; break; } }
	}
	if (vi == null) { return "{\"ok\":false,\"error\":\"no-VirtualInput\"}"; }
	const System.Reflection.BindingFlags Statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	// No `Install` call here on purpose: installing re-runs the patch setup, which clears the staged key table,
	// and the minigame reads that as the player letting go (`MinigameBase.handStoppedClicking`) — measured
	// 2026-11-23, `lock-36`: the target was alive with health 1000, and `lockpick-enter`'s own eval closed it.
	vi.GetMethod("MoveTo", Statics).Invoke(null, new object[] { screen.x, screen.y });
	var press = {{n:press}} != 0;
	// Pressing here cancels the minigame the crate click just started: the game reads the second click as another
	// drag on an open minigame (measured 2026-11-23, `lock-29`: the minigame was alive at 03:13:54.422 with
	// `handDistance 448.85`, and 50 ms later `lockpick-target` answered `no-minigame`). The row moves with
	// `press=0` and lets `lockpick-hold` do the pressing on the band.
	if (press) { vi.GetMethod("PressKey", Statics).Invoke(null, new object[] { UnityEngine.KeyCode.Mouse0 }); }

	var hand = main.handPos;
	var handAngle = UnityEngine.Mathf.Atan2(hand.y, hand.x) * UnityEngine.Mathf.Rad2Deg;
	if (handAngle < 0f) { handAngle += 360f; }

	return "{\"ok\":true,\"kind\":\"" + type.Name + "\""
		+ ",\"correctAngle\":" + angle.ToString("0.###", inv)
		+ ",\"uiScale\":" + scale.ToString("0.###", inv)
		+ ",\"baseX\":" + basePos.x.ToString("0.###", inv)
		+ ",\"baseY\":" + basePos.y.ToString("0.###", inv)
		+ ",\"targetX\":" + target.x.ToString("0.###", inv)
		+ ",\"targetY\":" + target.y.ToString("0.###", inv)
		+ ",\"screenX\":" + screen.x.ToString("0.###", inv)
		+ ",\"screenY\":" + screen.y.ToString("0.###", inv)
		+ ",\"handX\":" + hand.x.ToString("0.###", inv)
		+ ",\"handY\":" + hand.y.ToString("0.###", inv)
		+ ",\"handDistance\":" + hand.magnitude.ToString("0.###", inv)
		+ ",\"handAngle\":" + handAngle.ToString("0.###", inv)
		+ ",\"pressed\":" + (press ? "true" : "false") + "}";
}))()
