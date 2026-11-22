// recipe: aim-at-world
// args: x=n y=n
// returns: ok, iterations, errorX, errorY, distance, screenX, screenY, pointerX, pointerY, note, trace
//
// Aims the game's own pointer at a world point by closing the loop inside the client: stage a screen position, read the
// world point the game makes of that pointer (`Camera.ScreenToWorldPoint(Input.mousePosition)`), correct in pixels using
// the camera's own scale, and repeat until the gap is under the tolerance. `aim-at-cell` does the same for a block's
// centre; a door's usable collider sits at its own transform, which is not a block centre (measured 2026-11-23: aiming
// at the cell under the door named `containercrate(Clone)` under the pointer, so the use key landed on nothing).
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var worldX = (float)({{n:x}});
	var worldY = (float)({{n:y}});
	var cam = Camera.main;
	if (cam == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var findType = new System.Func<string, System.Type>((simpleName) => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { System.Type[] types; try { types = assemblies[i].GetTypes(); } catch { continue; } for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } } } return null; });
	var vi = findType("VirtualInput");
	if (vi == null) { return "{\"ok\":false,\"error\":\"no-VirtualInput\"}"; }
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	vi.GetMethod("Install", Flags).Invoke(null, new object[] { false });
	var moveTo = vi.GetMethod("MoveTo", Flags);
	var target = new Vector3(worldX, worldY, 0f);
	var screen = cam.WorldToScreenPoint(target);
	var stagedX = screen.x;
	var stagedY = screen.y;
	var errorX = 0f;
	var errorY = 0f;
	var iterations = 0;
	var trace = new System.Text.StringBuilder();
	for (; iterations < 6; iterations++)
	{
		moveTo.Invoke(null, new object[] { (float)stagedX, (float)stagedY });
		var back = cam.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
		errorX = target.x - back.x;
		errorY = target.y - back.y;
		trace.Append(iterations).Append(':').Append(stagedX.ToString("0.#", inv)).Append(',').Append(stagedY.ToString("0.#", inv))
			.Append('>').Append(back.x.ToString("0.###", inv)).Append(',').Append(back.y.ToString("0.###", inv))
			.Append("e").Append(errorX.ToString("0.###", inv)).Append(';');
		if ((errorX * errorX) + (errorY * errorY) < 0.09f) { break; }
		var here = cam.WorldToScreenPoint(back);
		var next = cam.WorldToScreenPoint(new Vector3(back.x + 1f, back.y + 1f, 0f));
		var perUnitX = next.x - here.x;
		var perUnitY = next.y - here.y;
		if (Math.Abs(perUnitX) < 0.0001f || Math.Abs(perUnitY) < 0.0001f) { break; }
		stagedX += errorX * perUnitX;
		stagedY += errorY * perUnitY;
	}
	var distance = (float)Math.Sqrt((errorX * errorX) + (errorY * errorY));
	return "{\"ok\":true"
		+ ",\"iterations\":" + iterations.ToString(inv)
		+ ",\"errorX\":" + errorX.ToString("0.####", inv)
		+ ",\"errorY\":" + errorY.ToString("0.####", inv)
		+ ",\"distance\":" + distance.ToString("0.####", inv)
		+ ",\"screenX\":" + stagedX.ToString("0.#", inv)
		+ ",\"screenY\":" + stagedY.ToString("0.#", inv)
		+ ",\"pointerX\":" + UnityEngine.Input.mousePosition.x.ToString("0.#", inv)
		+ ",\"pointerY\":" + UnityEngine.Input.mousePosition.y.ToString("0.#", inv)
		+ ",\"note\":\"aimed by the camera's own projection\""
		+ ",\"trace\":\"" + trace.ToString() + "\"}";
}))()
