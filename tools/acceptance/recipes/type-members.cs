// recipe: type-members
// args: x=n y=n radius=n type=s
// serves: guest-remote-pose-head-orientation-desync (the clone exposes `Limb`, not `Body`, so the facing data must be named on Limb)
// returns: ok, type, matched, entries (each path and members), error, detail
//
// Reads the SCALAR members of one component type on the objects within <radius> of (x, y): booleans, numbers, strings, enums and
// Vector2/Vector3 fields and properties, by name and value. `type` is the SHORT type name (Limb, LiquidAffect...). The point is
// to name the member that carries left/right facing and the look target, because the clone has no `Body` and the earlier probe
// was looking for exactly that.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var radius = (float)({{n:radius}});
	var wanted = {{s:type}};
	if (radius <= 0f) { radius = 3f; }
	var origin = new UnityEngine.Vector3(x, y, 0f);
	var all = UnityEngine.Object.FindObjectsOfType<UnityEngine.Component>();
	var entries = "";
	var matched = 0;
	for (var i = 0; i < all.Length; i++) {
		var comp = all[i];
		if (comp == null || comp.GetType().Name != wanted) { continue; }
		var t = comp.transform;
		if (t == null || !t.gameObject.activeInHierarchy) { continue; }
		if (UnityEngine.Vector3.Distance(t.position, origin) > radius) { continue; }
		matched++;
		if (matched > 12) { continue; }
		var members = "";
		var type = comp.GetType();
		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
		var fields = type.GetFields(flags);
		for (var f = 0; f < fields.Length; f++) {
			var field = fields[f];
			var ft = field.FieldType;
			var scalar = ft == typeof(bool) || ft == typeof(int) || ft == typeof(float) || ft == typeof(string)
				|| ft == typeof(UnityEngine.Vector2) || ft == typeof(UnityEngine.Vector3) || ft.IsEnum;
			if (!scalar) { continue; }
			string value;
			try { value = System.Convert.ToString(field.GetValue(comp), inv) ?? "null"; } catch { value = "?"; }
			members += (members.Length == 0 ? "" : "|") + field.Name + ":" + ft.Name + "=" + value.Replace("\"", "'");
		}
		var props = type.GetProperties(flags);
		for (var p = 0; p < props.Length; p++) {
			var prop = props[p];
			if (!prop.CanRead || prop.GetIndexParameters().Length != 0) { continue; }
			var pt = prop.PropertyType;
			var scalar = pt == typeof(bool) || pt == typeof(int) || pt == typeof(float) || pt == typeof(string)
				|| pt == typeof(UnityEngine.Vector2) || pt == typeof(UnityEngine.Vector3) || pt.IsEnum;
			if (!scalar) { continue; }
			string value;
			try { value = System.Convert.ToString(prop.GetValue(comp, null), inv) ?? "null"; } catch { value = "?"; }
			members += (members.Length == 0 ? "" : "|") + prop.Name + "~:" + pt.Name + "=" + value.Replace("\"", "'");
		}
		var chain = "";
		var walk = t;
		for (var depth = 0; depth < 5 && walk != null; depth++) {
			chain = walk.name + "/" + chain;
			walk = walk.parent;
		}
		entries += (entries.Length == 0 ? "" : ",") + "{\"path\":\"" + chain.Replace("\"", "'") + "\",\"members\":\"" + members + "\"}";
	}
	return "{\"ok\":true,\"type\":\"" + wanted + "\",\"matched\":" + matched.ToString(inv)
		+ ",\"entries\":[" + entries + "]}";
}))()
