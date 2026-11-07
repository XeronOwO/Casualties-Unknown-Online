// recipe: openable-state
// args: none
// returns: ok, openables, x, y, health, open, distance, openCount, openX, openY, error
//
// Reads the nearest lockable openable's own building health: `Openable.OnUse()` sets `BuildingEntity.health = 0` when it
// opens (reversing/Assembly-CSharp/Assembly-CSharp/Openable.cs:12), and the lockpick minigame reaches the same field through
// its `toDestroy` target. An opened openable may also leave the active hierarchy, so the recipe also counts every openable
// whose health is <= 0 (inactive included) and names the nearest of them. Read-only.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }

	var openables = UnityEngine.Object.FindObjectsOfType<Openable>(true);
	if (openables.Length == 0) { return "{\"ok\":false,\"error\":\"no-openable\",\"openables\":0}"; }

	var best = (Openable)null;
	var bestDistance = float.MaxValue;
	var openCount = 0;
	var openBest = (Openable)null;
	var openDistance = float.MaxValue;
	for (var i = 0; i < openables.Length; i++) {
		var candidate = openables[i];
		if (candidate == null) { continue; }
		var entity = candidate.GetComponent<BuildingEntity>();
		if (entity == null) { continue; }
		var distance = UnityEngine.Vector2.Distance(candidate.transform.position, body.transform.position);
		if (candidate.gameObject.activeInHierarchy && distance < bestDistance) { best = candidate; bestDistance = distance; }
		if (entity.health <= 0f) {
			openCount++;
			if (distance < openDistance) { openBest = candidate; openDistance = distance; }
		}
	}
	if (best == null) { return "{\"ok\":false,\"error\":\"no-live-openable\",\"openables\":" + openables.Length.ToString(inv) + "}"; }
	var entityBest = best.GetComponent<BuildingEntity>();
	var pos = best.transform.position;
	var openX = openBest == null ? -1f : openBest.transform.position.x;
	var openY = openBest == null ? -1f : openBest.transform.position.y;
	return "{\"ok\":true,\"openables\":" + openables.Length.ToString(inv)
		+ ",\"x\":" + pos.x.ToString("0.##", inv)
		+ ",\"y\":" + pos.y.ToString("0.##", inv)
		+ ",\"health\":" + entityBest.health.ToString("0.##", inv)
		+ ",\"open\":" + (entityBest.health <= 0f ? "true" : "false")
		+ ",\"distance\":" + bestDistance.ToString("0.#", inv)
		+ ",\"openCount\":" + openCount.ToString(inv)
		+ ",\"openX\":" + openX.ToString("0.##", inv)
		+ ",\"openY\":" + openY.ToString("0.##", inv)
		+ "}";
}))()
