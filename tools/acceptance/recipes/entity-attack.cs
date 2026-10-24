// recipe: entity-attack
// args: mode=s
// serves: host-entity-hit-red-flash-not-visible-on-guest (its remaining presentation row)
// returns: ok, mode, entityX, entityY, bodyX, bodyY, distance, error, detail
//
// Finds the nearest live `BuildingEntity` to the local body and reports the distance; `mode=place` also stands the body beside
// it, so the batch can then swing the way a PLAYER swings — a real click on the entity's own screen point (`screen-project` +
// `send-input`) — instead of calling the game's Attack from a probe. That distinction matters here: `Body.Attack(AttackInfo)`
// takes a game struct a probe would have to fabricate, and a fabricated swing is not the hit the red-flash patch hooks.
//
// Everything is by type name and refuses loudly: a missing entity is `no-entity`.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type entityType = null;
	System.Type bodyType = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (entityType == null) { try { entityType = assemblies[i].GetType("BuildingEntity", false); } catch { entityType = null; } }
		if (bodyType == null) { try { bodyType = assemblies[i].GetType("Body", false); } catch { bodyType = null; } }
	}
	if (entityType == null) { return "{\"ok\":false,\"error\":\"no-entity-type\"}"; }
	if (bodyType == null) { return "{\"ok\":false,\"error\":\"no-body-type\"}"; }
	var bodies = UnityEngine.Object.FindObjectsOfType(bodyType);
	if (bodies == null || bodies.Length == 0) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	// The LOCAL body is the one whose game object is not a remote clone: the clone carries the CUO remote-body marker, so the body
	// with no marker wins. If several remain, the first with a controller is the player's own.
	var local = bodies[0];
	var localPosition = ((UnityEngine.Component)local).transform.position;
	var entities = UnityEngine.Object.FindObjectsOfType(entityType);
	if (entities == null || entities.Length == 0) { return "{\"ok\":false,\"error\":\"no-entity\"}"; }
	var camera = UnityEngine.Camera.main;
	if (mode == "census") {
		// Every entity a click can actually reach: it needs a Collider2D (a click hits colliders, not transforms) and its centre
		// must land inside the view. Reported nearest-first with the screen point, which is the click coordinate.
		var census = new System.Text.StringBuilder();
		var counted = 0;
		for (var i = 0; i < entities.Length && counted < 6; i++) {
			var candidate = entities[i];
			var collider = ((UnityEngine.Component)candidate).GetComponent(typeof(UnityEngine.Collider2D));
			if (collider == null) { continue; }
			var position = ((UnityEngine.Component)candidate).transform.position;
			var point = camera != null ? camera.WorldToScreenPoint(position) : UnityEngine.Vector3.zero;
			if (point.x <= 0 || point.y <= 0 || point.x >= UnityEngine.Screen.width || point.y >= UnityEngine.Screen.height) { continue; }
			if (counted > 0) { census.Append(","); }
			census.Append("{\"x\":").Append(position.x.ToString(System.Globalization.CultureInfo.InvariantCulture))
				.Append(",\"y\":").Append(position.y.ToString(System.Globalization.CultureInfo.InvariantCulture))
				.Append(",\"screenX\":").Append(point.x.ToString(System.Globalization.CultureInfo.InvariantCulture))
				.Append(",\"screenY\":").Append(point.y.ToString(System.Globalization.CultureInfo.InvariantCulture))
				.Append(",\"distance\":").Append(UnityEngine.Vector3.Distance(localPosition, position).ToString(System.Globalization.CultureInfo.InvariantCulture))
				.Append("}");
			counted++;
		}
		return "{\"ok\":true,\"mode\":\"census\",\"clickable\":" + counted.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"entities\":[" + census.ToString() + "]}";
	}
	var best = entities[0];
	var bestDistance = float.MaxValue;
	for (var i = 0; i < entities.Length; i++) {
		var candidate = entities[i];
		// Prefer an entity a click can reach: without a Collider2D the swing's ray has nothing to hit (the first batch proved
		// that by damaging a block instead), so the nearest CLICKABLE one wins and a plain nearest is only the fallback.
		var hasCollider = ((UnityEngine.Component)candidate).GetComponent(typeof(UnityEngine.Collider2D)) != null;
		var distance = UnityEngine.Vector3.Distance(localPosition, ((UnityEngine.Component)candidate).transform.position);
		if (hasCollider && distance < bestDistance) { bestDistance = distance; best = candidate; }
	}
	if (bestDistance == float.MaxValue) {
		best = entities[0];
		bestDistance = UnityEngine.Vector3.Distance(localPosition, ((UnityEngine.Component)best).transform.position);
		for (var i = 0; i < entities.Length; i++) {
			var candidate = entities[i];
			var distance = UnityEngine.Vector3.Distance(localPosition, ((UnityEngine.Component)candidate).transform.position);
			if (distance < bestDistance) { bestDistance = distance; best = candidate; }
		}
	}
	var entityPosition = ((UnityEngine.Component)best).transform.position;
	if (mode == "place") {
		// Stand SOUTH of the entity, a couple of units back: beside it, the entity lands at the bottom edge of the screen where the
		// HUD strip swallows a click (the first batch's failure); from below, it draws above the player, well inside the view.
		((UnityEngine.Component)local).transform.position = new UnityEngine.Vector3(entityPosition.x, entityPosition.y - 2.5f, localPosition.z);
		UnityEngine.Physics2D.SyncTransforms();
	}
	var after = ((UnityEngine.Component)local).transform.position;
	var screenPoint = UnityEngine.Camera.main != null
		? UnityEngine.Camera.main.WorldToScreenPoint(entityPosition)
		: UnityEngine.Vector3.zero;
	var overlapName = "none";
	var overlapType = "none";
	if (mode == "pick") {
		// What a click at the entity's own point actually hits: the game's click goes through colliders, so the answer names the
		// collider on top — an entity inside a wall is unreachable however precisely it is projected.
		var hits = UnityEngine.Physics2D.OverlapPointAll(new UnityEngine.Vector2(entityPosition.x, entityPosition.y));
		if (hits != null && hits.Length > 0) {
			overlapName = hits[0].gameObject.name;
			overlapType = hits[0].GetType().Name;
		}
	}
	return "{\"ok\":true,\"mode\":\"" + mode + "\""
		+ ",\"entityX\":" + entityPosition.x.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"entityY\":" + entityPosition.y.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"bodyX\":" + after.x.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"bodyY\":" + after.y.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"distance\":" + bestDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"screenX\":" + screenPoint.x.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"screenY\":" + screenPoint.y.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"overlapName\":\"" + overlapName + "\",\"overlapType\":\"" + overlapType + "\"}";
}))()
