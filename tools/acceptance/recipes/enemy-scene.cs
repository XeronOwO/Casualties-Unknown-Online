// recipe: enemy-scene
// args: none
// serves: enemy-generation-pairing-late-join rows 2, 4 and 6 (the member's bound copies, the runtime spawn's position, the 20 Hz drive)
// returns: ok, utc, count, bodyX, bodyY, alive, conscious, animals (iid, id, x, y, health, runtime, bodyDistance), error
//
// Every animal THIS client's scene holds, with the Unity instance id (stable across reads, so one animal can be
// followed), its prefab id, position, health and the runtime-creation sequence when the copy rode the creation
// channel. The enemy table's own `enemy-table-read` gives the authoritative rows; this recipe gives the scene
// copies those rows are bound to. Read-only.
//
// In-world only: a client with no local body answers ok=false / error=no-local-body, because an empty animal
// list would otherwise read as "this layer has no enemies" rather than "this client is not in a world".
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var bx = body.transform.position.x;
	var by = body.transform.position.y;
	var animals = UnityEngine.Object.FindObjectsOfType<BuildingEntity>(true);
	var sb = new System.Text.StringBuilder();
	var count = 0;
	for (var i = 0; i < animals.Length; i++) {
		var e = animals[i];
		if (e == null || !e.animal) { continue; }
		var p = e.transform.position;
		var runtime = "null";
		var creation = e.GetComponent("CasualtiesUnknownOnline.GameAdapter.World.RuntimeEntityCreation");
		if (creation != null) {
			var property = creation.GetType().GetProperty("CreationSequence", flags);
			if (property == null) {
				runtime = "\"unavailable\"";
			} else {
				var seq = property.GetValue(creation, null);
				runtime = "\"" + System.Convert.ToString(seq, inv) + "\"";
			}
		}
		if (count > 0) { sb.Append(','); }
		count = count + 1;
		var idText = e.id.Replace("\\", "\\\\").Replace("\"", "\\\"");
		sb.Append("{\"iid\":").Append(e.GetInstanceID().ToString(inv))
			.Append(",\"id\":\"").Append(idText).Append('"')
			.Append(",\"x\":").Append(p.x.ToString("0.###", inv))
			.Append(",\"y\":").Append(p.y.ToString("0.###", inv))
			.Append(",\"health\":").Append(e.health.ToString("0.###", inv))
			.Append(",\"runtime\":").Append(runtime)
			.Append(",\"bodyDistance\":").Append(((float)System.Math.Sqrt((p.x - bx) * (p.x - bx) + (p.y - by) * (p.y - by))).ToString("0.###", inv))
			.Append('}');
	}
	return "{\"ok\":true,\"utc\":\"" + System.DateTime.UtcNow.ToString("o") + "\",\"count\":" + count.ToString(inv)
		+ ",\"bodyX\":" + bx.ToString("0.###", inv) + ",\"bodyY\":" + by.ToString("0.###", inv)
		+ ",\"alive\":" + (body.alive ? "true" : "false")
		+ ",\"conscious\":" + (body.conscious ? "true" : "false")
		+ ",\"animals\":[" + sb.ToString() + "]}";
}))()
