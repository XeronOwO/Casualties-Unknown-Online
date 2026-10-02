// recipe: runtime-entity-move
// args: sequence=s dx=n dy=n
// serves: runtime-entity-creation-rejection row 5
// returns: ok, sequence, prefab, x0, y0, x1, y1, error, detail
//
// Moves ONE runtime creation by (dx, dy), located by its stamped key, so the copy sits outside its
// creation cell before the host's rejection arrives: the rejection must find it by its creation key,
// never by position. The rigidbody is moved with the transform and its velocity zeroed so physics cannot
// snap the copy back inside the read window. Read-only apart from that one transform write.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var targetSequence = System.Convert.ToUInt32({{s:sequence}}, inv);
	var all = UnityEngine.Object.FindObjectsOfType<BuildingEntity>(true);
	BuildingEntity target = null;
	for (var i = 0; i < all.Length; i++) {
		var e = all[i];
		if (e == null) { continue; }
		var comp = e.GetComponent("CasualtiesUnknownOnline.GameAdapter.World.RuntimeEntityCreation");
		if (comp == null) { continue; }
		var seq = System.Convert.ToUInt32(comp.GetType().GetProperty("CreationSequence", flags).GetValue(comp, null));
		if (seq == targetSequence) { target = e; break; }
	}
	if (target == null) {
		return "{\"ok\":false,\"error\":\"no-target\",\"detail\":\"no runtime creation with sequence " + targetSequence.ToString(inv) + "\"}";
	}
	var comp2 = target.GetComponent("CasualtiesUnknownOnline.GameAdapter.World.RuntimeEntityCreation");
	var prefab = System.Convert.ToString(comp2.GetType().GetProperty("Id", flags).GetValue(comp2, null));
	var p0 = target.transform.position;
	var x1 = p0.x + (float)({{n:dx}});
	var y1 = p0.y + (float)({{n:dy}});
	var v = new UnityEngine.Vector3(x1, y1, p0.z);
	target.transform.position = v;
	var body = target.GetComponent<UnityEngine.Rigidbody2D>();
	if (body != null) {
		body.position = new UnityEngine.Vector2(x1, y1);
		body.velocity = UnityEngine.Vector2.zero;
	}
	var p1 = target.transform.position;
	return "{\"ok\":true,\"sequence\":" + targetSequence.ToString(inv)
		+ ",\"prefab\":\"" + prefab + "\""
		+ ",\"x0\":" + p0.x.ToString("0.000", inv) + ",\"y0\":" + p0.y.ToString("0.000", inv)
		+ ",\"x1\":" + p1.x.ToString("0.000", inv) + ",\"y1\":" + p1.y.ToString("0.000", inv) + "}";
}))()
