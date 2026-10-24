// recipe: clone-components
// args: x=n y=n radius=n
// serves: guest-remote-pose-head-orientation-desync (the row needs the remote clone's REAL type before any facing field can be read)
// returns: ok, scanned, found, entries (each distance, name, path, components), error, detail
//
// Names what renders another member on THIS client. The earlier probe assumed a `Body` component and found none on either
// client, so this one starts from the Transform every renderer has: it scans every loaded object, keeps the ones within
// <radius> world units of (x, y), and reports each one's component TYPE NAMES plus its hierarchy path. No assumption is made
// about which component carries the pose; the types are the reading.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var radius = (float)({{n:radius}});
	if (radius <= 0f) { radius = 3f; }
	var origin = new UnityEngine.Vector3(x, y, 0f);
	var all = UnityEngine.Object.FindObjectsOfType<UnityEngine.Transform>();
	var entries = "";
	var found = 0;
	for (var i = 0; i < all.Length; i++) {
		var t = all[i];
		if (t == null || !t.gameObject.activeInHierarchy) { continue; }
		var d = UnityEngine.Vector3.Distance(t.position, origin);
		if (d > radius) { continue; }
		found++;
		if (found > 40) { continue; }
		var chain = "";
		var walk = t;
		for (var depth = 0; depth < 6 && walk != null; depth++) {
			chain = walk.name + "/" + chain;
			walk = walk.parent;
		}
		var comps = "";
		var list = t.GetComponents<UnityEngine.Component>();
		for (var c = 0; c < list.Length && c < 24; c++) {
			var comp = list[c];
			if (comp == null) { continue; }
			comps += (comps.Length == 0 ? "" : "|") + comp.GetType().FullName;
		}
		entries += (entries.Length == 0 ? "" : ",") + "{\"distance\":" + d.ToString("0.##", inv)
			+ ",\"name\":\"" + t.name.Replace("\"", "'")
			+ "\",\"path\":\"" + chain.Replace("\"", "'")
			+ "\",\"components\":\"" + comps + "\"}";
	}
	return "{\"ok\":true,\"scanned\":" + all.Length.ToString(inv)
		+ ",\"found\":" + found.ToString(inv)
		+ ",\"entries\":[" + entries + "]}";
}))()
