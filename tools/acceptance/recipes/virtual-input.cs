// recipe: virtual-input
// args: mode=s x=n y=n
// serves: cross-player-drag-use-feedback, use-drag-out-of-a-backpack-drops-the-item, context-menu-does-not-block-the-pointer (the rows a batch could not aim)
// returns: ok, mode, installed, enabled, pointer, realPointer, answer, error, detail
//
// The acceptance channel's virtual pointer: installs the Harmony postfix on UnityEngine.Input.mousePosition (a MANAGED getter on this build, unlike the
// mouse-button methods, which are internal calls with no IL), then stages a screen position the game's own reads answer instead of the real mouse.
//  - mode=install:   patch the getter; the answer says what was patched or why it could not be
//  - mode=move:      stage (x, y) — the game's own screen coordinates, origin bottom-left
//  - mode=clear:     drop the staged pointer; the real one answers again, the patch stays
//  - mode=uninstall: remove the patch and the staged pointer
//  - mode=report:    the staged pointer, the real one, and what Input.mousePosition now answers
//
// Why this shape: OS-level input costs the user a permission every batch, and there is no other way to aim a row at a screen position the batch cannot
// reach with a real mouse. The reflex to check first is whether the patch took at all — a getter the JIT inlined answers the real pointer even with the
// postfix installed, which is why every mode reports both values.
//
// Why reflection: `VirtualInput` is internal to the adapter, so the type is looked up BY NAME like the other adapter surfaces these recipes reach.
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
	var virtualInput = findType("VirtualInput");
	if (virtualInput == null) { return "{\"ok\":false,\"error\":\"no-VirtualInput\"}"; }
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	if (mode == "install" || mode == "move" || mode == "clear" || mode == "uninstall") {
		string answer = "";
		if (mode == "install") {
			var install = virtualInput.GetMethod("Install", Flags);
			var args = new object[] { false };
			answer = (string)install.Invoke(null, args);
		} else if (mode == "move") {
			var install = virtualInput.GetMethod("Install", Flags);
			install.Invoke(null, new object[] { false });
			answer = "moved";
		} else if (mode == "clear") {
			virtualInput.GetMethod("Clear", Flags).Invoke(null, null);
			answer = "cleared";
		} else {
			virtualInput.GetMethod("Uninstall", Flags).Invoke(null, null);
			answer = "uninstalled";
		}
		if (mode == "move") {
			virtualInput.GetMethod("MoveTo", Flags).Invoke(null, new object[] { x, y });
		}
	}
	var installed = (bool)virtualInput.GetProperty("Installed", Flags).GetValue(null, null);
	var enabled = (bool)virtualInput.GetProperty("Enabled", Flags).GetValue(null, null);
	var hasPointer = false;
	var pointerX = 0f;
	var pointerY = 0f;
	var tryGet = virtualInput.GetMethod("TryGetStaged", Flags);
	if (tryGet != null) {
		var args = new object[] { null };
		if ((bool)tryGet.Invoke(null, args)) {
			var staged = (UnityEngine.Vector3)args[0];
			hasPointer = true;
			pointerX = staged.x;
			pointerY = staged.y;
		}
	}
	var real = UnityEngine.Input.mousePosition;
	return "{\"ok\":true,\"mode\":\"" + mode + "\""
		+ ",\"installed\":" + (installed ? "true" : "false")
		+ ",\"enabled\":" + (enabled ? "true" : "false")
		+ ",\"pointer\":\"" + (hasPointer ? pointerX.ToString("0.###", inv) + "," + pointerY.ToString("0.###", inv) : "none") + "\""
		+ ",\"inputAnswers\":\"" + real.x.ToString("0.###", inv) + "," + real.y.ToString("0.###", inv) + "\""
		+ "}";
}))()
