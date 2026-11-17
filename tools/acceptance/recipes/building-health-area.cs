// recipe: building-health-area
// args: x=n y=n r=n all=n
// serves: the damaged-building rows of a save/continue run. The full census is too large for one eval
//         answer (it truncates), and the restore applier addresses a building by the position the cut
//         recorded, so a restore reading needs only the buildings around one point: their position, their
//         health and (with all=1) whether the entity is active in the hierarchy — the difference between
//         "the regenerated world does not have it" and "it is there, in a deactivated chunk".
// returns: ok, total (buildings inside the radius), entries (each [x, y, health, active])
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var r = (float)({{n:r}});
	var all = (int)({{n:all}});
	if (r <= 0f) { r = 16f; }
	var r2 = r * r;
	var found = all != 0
		? UnityEngine.Object.FindObjectsOfType<BuildingEntity>(true)
		: UnityEngine.Object.FindObjectsOfType<BuildingEntity>();
	if (found == null) { return "{\"ok\":false,\"error\":\"no-census\"}"; }
	var entries = new System.Collections.Generic.List<string>();
	var total = 0;
	foreach (var building in found) {
		if (building == null) { continue; }
		var pos = building.transform.position;
		var dx = pos.x - x;
		var dy = pos.y - y;
		if (dx * dx + dy * dy > r2) { continue; }
		total++;
		entries.Add("[" + pos.x.ToString("R", inv)
			+ "," + pos.y.ToString("R", inv)
			+ "," + building.health.ToString("R", inv)
			+ "," + (building.gameObject.activeInHierarchy ? "1" : "0") + "]");
	}
	return "{\"ok\":true,\"total\":" + total.ToString(inv) + ",\"entries\":[" + string.Join(",", entries) + "]}";
}))()
