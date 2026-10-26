// recipe: stage-pointer
// args: mode=s x=n y=n
// serves: use-drag-out-of-a-backpack-drops-the-item, cross-player-drag-use-feedback (the centre-use rows a batch could not drive)
// returns: ok, mode, x, y, staged, error, detail
//
// Stages the pointer `DragUseCenter.IsUnderPointer` reads — the ONE guard a batch could not satisfy, because it read `Input.mousePosition` directly
// (measured 2026-10-26: three release attempts onto the displayed backpack's centre all answered "produced no intent"). `mode=centre` uses the live
// radial circle's own position (the centre circle the player aims at), `mode=at` takes x/y, and `mode=clear` returns the guard to the real pointer.
// The caller clears it after ONE release, so a staged pointer never outlives the gesture it was staged for.
//
// Why reflection: `DragUseCenter` is internal to the adapter and an in-process eval cannot compile against it; the type and its methods are looked up
// BY NAME, like every other adapter surface these recipes reach.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var x = {{n:x}};
	var y = {{n:y}};
	var findType = new System.Func<string, System.Type>((simpleName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) {
				if (types[t].Name == simpleName) { return types[t]; }
			}
		}
		return null;
	});
	var centerType = findType("DragUseCenter");
	if (centerType == null) { return "{\"ok\":false,\"error\":\"no-DragUseCenter\"}"; }
	var stage = centerType.GetMethod("StagePointer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	if (stage == null) { return "{\"ok\":false,\"error\":\"no-StagePointer\"}"; }
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	if (mode == "clear") {
		stage.Invoke(null, new object[] { null });
		return "{\"ok\":true,\"mode\":\"clear\",\"staged\":false}";
	}
	float px;
	float py;
	if (mode == "centre" || mode == "center") {
		var camera = PlayerCamera.main;
		if (camera == null || camera.radialCircle == null) { return "{\"ok\":false,\"error\":\"no-radial-circle\"}"; }
		var position = camera.radialCircle.transform.position;
		px = position.x;
		py = position.y;
	} else if (mode == "at") {
		px = x;
		py = y;
	} else {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be centre, at or clear\"}";
	}
	var staged = System.Activator.CreateInstance(
		findType("Vector2") ?? typeof(UnityEngine.Vector2),
		new object[] { px, py });
	stage.Invoke(null, new object[] { staged });
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"x\":" + px.ToString("0.###", inv) + ",\"y\":" + py.ToString("0.###", inv) + ",\"staged\":true}";
}))()
