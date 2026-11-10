// recipe: lockpick-press
// args: x=n y=n
// serves: 1.0-regression-sweep (the lockpick case)
// returns: ok, x, y
//
// Stages the pointer and presses the attack key IN THE SAME EVAL, so the game's next frame reads a
// pointer already on the ring and a key already down. The separate move/press recipes leave a frame
// between them; the minigame's hand can fall back to the real mouse in that gap and the press lands
// outside the ring (the lockpick case's measured oscillation, 2026-11-11).
((System.Func<string>)(() => {
	var x = {{n:x}};
	var y = {{n:y}};
	var findType = new System.Func<string, System.Type>((simpleName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } }
		}
		return null;
	});
	var vi = findType("VirtualInput");
	if (vi == null) { return "{\"ok\":false,\"error\":\"no-VirtualInput\"}"; }
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	vi.GetMethod("Install", Flags).Invoke(null, new object[] { false });
	vi.GetMethod("MoveTo", Flags).Invoke(null, new object[] { (float)x, (float)y });
	vi.GetMethod("PressKey", Flags).Invoke(null, new object[] { UnityEngine.KeyCode.Mouse0 });
	return "{\"ok\":true,\"x\":" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"y\":" + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
