// recipe: member-position
// args: x=n y=n radius=n name=s
// serves: cross-player-eat-skips-native-steps (the drag release needs the teammate's pixel, which needs its world point)
// returns: ok, found, name, x, y, distance, candidates, error, detail
//
// The nearest object with the given NAME around (x, y) within <radius>, skipping everything under the LOCAL body's own hierarchy, with its WORLD point.
// The name comes from the read that knows it: `remote-hand` lists what renders another member (`Body` on this build), while a guess like "anything named
// Clone" finds decoration clones instead (measured 2026-10-28: `wallholes(Clone)` answered before the teammate did). `screen-point2` turns the point into
// the pixel a drag is released at.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var radius = (float)({{n:radius}});
	var wanted = {{s:name}};
	if (radius <= 0f) { radius = 30f; }
	var origin = new UnityEngine.Vector3(x, y, -3f);
	var local = (UnityEngine.Transform)null;
	var camera = GameObject.FindObjectOfType(System.Type.GetType("PlayerCamera, Assembly-CSharp"));
	if (camera != null) {
		var bodyField = camera.GetType().GetField("body");
		var body = bodyField == null ? null : bodyField.GetValue(camera) as UnityEngine.Component;
		if (body != null) { local = body.transform; }
	}
	var all = UnityEngine.Object.FindObjectsOfType<UnityEngine.Transform>();
	var bestName = "";
	var bestDistance = -1f;
	var bestX = 0f;
	var bestY = 0f;
	var candidates = 0;
	for (var i = 0; i < all.Length; i++) {
		var t = all[i];
		if (t == null || !t.gameObject.activeInHierarchy) { continue; }
		if (!string.Equals(t.name, wanted, System.StringComparison.Ordinal)) { continue; }
		if (local != null && (t == local || t.IsChildOf(local) || local.IsChildOf(t))) { continue; }
		var d = UnityEngine.Vector3.Distance(t.position, origin);
		if (d > radius) { continue; }
		candidates++;
		if (bestDistance < 0f || d < bestDistance) {
			bestDistance = d;
			bestName = t.name;
			bestX = t.position.x;
			bestY = t.position.y;
		}
	}
	return "{\"ok\":true,\"found\":" + (bestName.Length == 0 ? "false" : "true")
		+ ",\"name\":\"" + bestName.Replace("\"", "'") + "\""
		+ ",\"x\":" + bestX.ToString("0.###", inv)
		+ ",\"y\":" + bestY.ToString("0.###", inv)
		+ ",\"distance\":" + bestDistance.ToString("0.###", inv)
		+ ",\"candidates\":" + candidates + "}";
}))()
