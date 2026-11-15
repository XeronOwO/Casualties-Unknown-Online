// recipe: attack-probe
// args: none
// returns: ok, before, after, rotAfter, entries, error
//
// Calls the game's own PlayerCamera.HandleAttacks once after staging the native
// attack key. The in-process input patch cannot observe the call when Mono inlines
// it into HandleInput, so this probe separates the gate/body path from the call-site
// observation; it is a reading, not a replacement for the staged case.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var findType = new System.Func<string, System.Type>((simpleName) =>
	{
		foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
		{
			System.Type[] types;
			try { types = assembly.GetTypes(); } catch { continue; }
			foreach (var type in types)
			{
				if (type.Name == simpleName) { return type; }
			}
		}
		return null;
	});
	var virtualInput = findType("VirtualInput");
	if (virtualInput == null) { return "{\"ok\":false,\"error\":\"no-VirtualInput\"}"; }
	var install = (string)virtualInput.GetMethod("Install", flags).Invoke(null, new object[] { false });
	var console = ConsoleScript.instance;
	if (console != null && console.active)
	{
		console.ToggleActiveState();
		var canvasField = typeof(ConsoleScript).GetField("consoleCanvas", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		var canvas = canvasField == null ? null : canvasField.GetValue(console) as UnityEngine.GameObject;
		if (canvas != null) { canvas.SetActive(false); }
	}
	virtualInput.GetMethod("MoveTo", flags).Invoke(null, new object[] { -100f, -100f });
	virtualInput.GetMethod("PressKey", flags).Invoke(null, new object[] { UnityEngine.KeyCode.Mouse0 });
	var before = body.attackCooldown;
	var entriesBefore = (int)virtualInput.GetProperty("HandleAttacksEntries", flags).GetValue(null, null);
	var method = typeof(PlayerCamera).GetMethod("HandleAttacks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
	if (method == null) { return "{\"ok\":false,\"error\":\"no-HandleAttacks\"}"; }
	method.Invoke(camera, null);
	var after = body.attackCooldown;
	var entriesAfter = (int)virtualInput.GetProperty("HandleAttacksEntries", flags).GetValue(null, null);
	return "{\"ok\":true,\"before\":" + before.ToString("0.###", inv)
		+ ",\"after\":" + after.ToString("0.###", inv)
		+ ",\"rotAfter\":" + body.attackRot.ToString("0.###", inv)
		+ ",\"entries\":" + (entriesAfter - entriesBefore)
		+ ",\"blockItemUsage\":" + (camera.blockItemUsage ? "true" : "false")
		+ ",\"forceNoMovement\":" + (body.forceNoMovement ? "true" : "false")
		+ ",\"conscious\":" + (body.conscious ? "true" : "false")
		+ ",\"woundView\":" + (camera.woundView != null && camera.woundView.activeSelf ? "true" : "false")
		+ ",\"pointerOverUI\":" + (UIUtil.IsPointerOverUIElement() ? "true" : "false")
		+ ",\"minigame\":" + (MinigameBase.main != null && MinigameBase.main.currentMinigame != null ? "true" : "false")
		+ ",\"liquid\":" + (LiquidTransfer.instance != null ? "true" : "false")
		+ ",\"keyQueries\":" + ((int)virtualInput.GetProperty("KeyQueries", flags).GetValue(null, null))
		+ ",\"keyDownAnswers\":" + ((int)virtualInput.GetProperty("KeyDownAnswers", flags).GetValue(null, null))
		+ ",\"install\":\"" + (install ?? "").Replace("\\", "/").Replace("\"", "''") + "\""
		+ "}";
}))()
