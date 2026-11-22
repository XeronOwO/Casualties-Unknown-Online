// recipe: swing-state
// args: none
// returns: ok, bodyX, bodyY, lookX, lookY, handItem, handSlot, allowUseItem, held, aimDegrees, blockItemUsage, minigame, woundView, uiOverPointer
//
// Names every gate between "the row holds the attack" and "a block takes damage": the body's aim point, whether it may
// use an item at all, what its hand holds, and the four early returns at the top of `PlayerCamera.HandleAttacks`
// (PlayerCamera.cs:1995-1997). A hold that never damages has to say which of those says no.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var hand = body.GetItem(body.handSlot);
	var look = body.targetLookPos;
	// Where the pointer came from, read in the same call as the game's look: the staged point, what
	// `Input.mousePosition` answers, and what `Camera.main` makes of it. The game's look is the last of those
	// (`PlayerCamera.cs:985`), so a look that disagrees with the projection means the game read another pointer.
	var input = UnityEngine.Input.mousePosition;
	var pointerWorld = Camera.main == null ? Vector3.zero : Camera.main.ScreenToWorldPoint(input);
	var stagedX = "none";
	var stagedY = "none";
	var held = UnityEngine.Input.GetKey(KeyBinds.GetBind("attack"));
	var ui = UIUtil.IsPointerOverUIElement();
	var dx = look.x - body.transform.position.x;
	var dy = look.y - body.transform.position.y;
	var aim = Math.Atan2(dy, dx) * 180.0 / Math.PI;
	var mini = MinigameBase.main;
	var swings = -1L;
	var uses = -1L;
	var seamText = "unknown";
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var a = 0; a < assemblies.Length; a++)
	{
		System.Type[] types;
		try { types = assemblies[a].GetTypes(); } catch { continue; }
		for (var t = 0; t < types.Length; t++)
		{
			if (types[t].Name != "VirtualInput") { continue; }
			var counter = types[t].GetProperty("Swings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			if (counter != null)
			{
				swings = System.Convert.ToInt64(counter.GetValue(null, null), System.Globalization.CultureInfo.InvariantCulture);
			}
			var used = types[t].GetProperty("Uses", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			if (used != null)
			{
				uses = System.Convert.ToInt64(used.GetValue(null, null), System.Globalization.CultureInfo.InvariantCulture);
			}
			var seam = types[t].GetProperty("SwingSeam", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			if (seam != null)
			{
				seamText = System.Convert.ToString(seam.GetValue(null, null), System.Globalization.CultureInfo.InvariantCulture);
			}
			var tryGet = types[t].GetMethod("TryGetStaged", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			if (tryGet != null)
			{
				var buffer = new object[] { null };
				if ((bool)tryGet.Invoke(null, buffer))
				{
					var point = (UnityEngine.Vector3)buffer[0];
					stagedX = point.x.ToString("0.###", inv);
					stagedY = point.y.ToString("0.###", inv);
				}
			}
		}
	}
	return "{\"ok\":true"
		+ ",\"bodyX\":" + body.transform.position.x.ToString("0.###", inv)
		+ ",\"bodyY\":" + body.transform.position.y.ToString("0.###", inv)
		+ ",\"lookX\":" + look.x.ToString("0.###", inv)
		+ ",\"lookY\":" + look.y.ToString("0.###", inv)
		+ ",\"handItem\":\"" + (hand == null ? "" : hand.name) + "\""
		+ ",\"handSlot\":" + body.handSlot.ToString(inv)
		+ ",\"allowUseItem\":" + (body.allowUseItem ? "true" : "false")
		+ ",\"held\":" + (held ? "true" : "false")
		+ ",\"aimDegrees\":" + aim.ToString("0.#", inv)
		+ ",\"blockItemUsage\":" + (camera.blockItemUsage ? "true" : "false")
		+ ",\"minigame\":" + (mini != null && mini.currentMinigame != null ? "true" : "false")
		+ ",\"woundView\":" + (camera.woundView != null && camera.woundView.activeSelf ? "true" : "false")
		+ ",\"uiOverPointer\":" + (ui ? "true" : "false")
		+ ",\"swings\":" + swings.ToString(inv)
		+ ",\"uses\":" + uses.ToString(inv)
		+ ",\"stagedX\":\"" + stagedX + "\",\"stagedY\":\"" + stagedY + "\""
		+ ",\"pointerX\":\"" + input.x.ToString("0.###", inv) + "\",\"pointerY\":\"" + input.y.ToString("0.###", inv) + "\""
		+ ",\"pointerWorldX\":\"" + pointerWorld.x.ToString("0.###", inv) + "\",\"pointerWorldY\":\"" + pointerWorld.y.ToString("0.###", inv) + "\""
		+ ",\"seam\":\"" + seamText.Replace("\"", "'") + "\"}";
}))()
