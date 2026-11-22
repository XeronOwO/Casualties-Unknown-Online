// recipe: move-openable
// args: none
// returns: ok, name, x, y, health, precision, moved
//
// Brings the nearest locked door to the host instead of bringing the host to the door: a fresh world keeps its doors
// hundreds of units away and `teleport` clamps to a floor (`lock-13`/`lock-14`), while the door object itself can simply be
// moved onto the floor the body already stands on. The row then aims at it and clicks it, exactly as a player would.
//
// Doors are scene objects, not `Resources` prefabs (`lock-15`: every `door*` resource name missed, only `containercrate`
// loaded), so this is the only way to get one within reach.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var cameras = UnityEngine.Object.FindObjectsOfType<PlayerCamera>();
	if (cameras == null || cameras.Length == 0) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = cameras[0].body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var findType = new System.Func<string, System.Type>((simpleName) => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { System.Type[] types; try { types = assemblies[i].GetTypes(); } catch { continue; } for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } } } return null; });
	var openableType = findType("Openable");
	if (openableType == null) { return "{\"ok\":false,\"error\":\"no-Openable\"}"; }
	var all = UnityEngine.Object.FindObjectsOfType(openableType);
	var here = body.transform.position;
	var best = (UnityEngine.Component)null;
	var bestDistance = float.MaxValue;
	for (var i = 0; i < all.Length; i++)
	{
		var openable = (UnityEngine.Component)all[i];
		if (openable == null) { continue; }
				var type = openable.GetType();
		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
		var instant = type.GetField("instant", flags) ?? type.GetField("instantOpen", flags);
		var keypad = type.GetField("isKeypad", flags) ?? type.GetField("keypad", flags) ?? type.GetField("needsKeypad", flags);
		var isInstant = instant != null && System.Convert.ToBoolean(instant.GetValue(openable), inv);
		var isKeypad = keypad != null && System.Convert.ToBoolean(keypad.GetValue(openable), inv);
		if (isInstant || isKeypad) { continue; }
		var building = openable.GetComponent("BuildingEntity");
		if (building != null)
		{
			var healthProperty = building.GetType().GetProperty("health", flags);
			var health = healthProperty == null ? -1f : System.Convert.ToSingle(healthProperty.GetValue(building, null), inv);
			if (health <= 0f) { continue; }
		}
		var distance = UnityEngine.Vector2.Distance(new UnityEngine.Vector2(here.x, here.y), new UnityEngine.Vector2(openable.transform.position.x, openable.transform.position.y));
		if (distance < bestDistance) { bestDistance = distance; best = openable; }
	}
	if (best == null) { return "{\"ok\":false,\"error\":\"no-locked-door\",\"seen\":" + all.Length.ToString(inv) + "}"; }
	var spot = new UnityEngine.Vector3(here.x + 3.5f, here.y, best.transform.position.z);
	best.transform.position = spot;
	var landed = best.transform.position;
	var health2 = -1f;
	var be = best.GetComponent("BuildingEntity");
	if (be != null) { var healthProperty = be.GetType().GetProperty("health", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); if (healthProperty != null) { health2 = System.Convert.ToSingle(healthProperty.GetValue(be, null), inv); } }
	return "{\"ok\":true"
		+ ",\"name\":\"" + best.name.Replace("\"", "''") + "\""
		+ ",\"x\":" + landed.x.ToString("0.###", inv)
		+ ",\"y\":" + landed.y.ToString("0.###", inv)
		+ ",\"health\":" + health2.ToString("0.##", inv)
		+ ",\"movedFrom\":" + bestDistance.ToString("0.##", inv) + "}";
}))()
