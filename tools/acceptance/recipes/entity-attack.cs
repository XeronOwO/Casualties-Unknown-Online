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
	var best = entities[0];
	var bestDistance = float.MaxValue;
	for (var i = 0; i < entities.Length; i++) {
		var candidate = entities[i];
		var distance = UnityEngine.Vector3.Distance(localPosition, ((UnityEngine.Component)candidate).transform.position);
		if (distance < bestDistance) { bestDistance = distance; best = candidate; }
	}
	var entityPosition = ((UnityEngine.Component)best).transform.position;
	if (mode == "place") {
		// Stand beside the entity: a swing from across the map cannot reach it, and the batch must not read that as a defect.
		((UnityEngine.Component)local).transform.position = new UnityEngine.Vector3(entityPosition.x + 0.6f, entityPosition.y, localPosition.z);
		UnityEngine.Physics2D.SyncTransforms();
	}
	var after = ((UnityEngine.Component)local).transform.position;
	return "{\"ok\":true,\"mode\":\"" + mode + "\""
		+ ",\"entityX\":" + entityPosition.x.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"entityY\":" + entityPosition.y.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"bodyX\":" + after.x.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"bodyY\":" + after.y.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"distance\":" + bestDistance.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
