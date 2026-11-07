// recipe: openable-census
// args: none
// returns: ok, total, groups, error
//
// Groups every `Openable` in THIS client's scene by the prefab id its `BuildingEntity` carries, and reports one
// sample position per group. It answers what the lockpick minigame's targets actually ARE on this build (a door, a
// hatch, a container, a machine) instead of naming them from memory. Read-only.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var openables = UnityEngine.Object.FindObjectsOfType<Openable>(true);
	if (openables == null || openables.Length == 0) { return "{\"ok\":true,\"total\":0,\"groups\":[]}"; }
	var byId = new System.Collections.Generic.Dictionary<string, int>();
	var sample = new System.Collections.Generic.Dictionary<string, UnityEngine.Vector2>();
	for (var i = 0; i < openables.Length; i++) {
		var candidate = openables[i];
		if (candidate == null) { continue; }
		var entity = candidate.GetComponent<BuildingEntity>();
		var id = entity == null || entity.id == null ? "?" : entity.id;
		if (!byId.ContainsKey(id)) { byId[id] = 0; sample[id] = candidate.transform.position; }
		byId[id] = byId[id] + 1;
	}
	var parts = new System.Collections.Generic.List<string>();
	foreach (var pair in byId) {
		var pos = sample[pair.Key];
		parts.Add("{\"id\":\"" + pair.Key + "\",\"count\":" + pair.Value.ToString(inv)
			+ ",\"x\":" + pos.x.ToString("0.#", inv) + ",\"y\":" + pos.y.ToString("0.#", inv) + "}");
	}
	return "{\"ok\":true,\"total\":" + openables.Length.ToString(inv) + ",\"groups\":[" + string.Join(",", parts) + "]}";
}))()
