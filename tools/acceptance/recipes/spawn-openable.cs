// recipe: spawn-openable
// args: names=s
// returns: ok, name, x, y, health, tried
//
// Puts a locked door of the game's own making next to the host, because a fresh world has its doors hundreds of units
// away and `teleport` clamps to a floor (`lock-13`/`lock-14`: the body landed 30-300 units from the request), while a
// door is the only thing that starts the lockpick minigame (`Openable.OnUse`, `Openable.cs:8-31`). The prefab comes from
// `UnityEngine.Resources.Load`, the same way `trap-stage` puts a landmine and a crate in the world.
//
// `names` is a comma separated candidate list, tried in order; the first prefab that loads and carries a lockable
// `Openable` wins. The caller learns the real name from `name` so the row never guesses again.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var wanted = ({{s:names}} ?? "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
	var cam = (PlayerCamera)null;
	var cameras = UnityEngine.Object.FindObjectsOfType<PlayerCamera>();
	if (cameras == null || cameras.Length == 0) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	cam = cameras[0];
	var body = cam.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var at = body.transform.position;
	var spot = new Vector3(at.x + 2f, at.y, 0f);
	var tried = new System.Text.StringBuilder();
	var findType = new System.Func<string, System.Type>((simpleName) => { var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { System.Type[] types; try { types = assemblies[i].GetTypes(); } catch { continue; } for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } } } return null; });
	var openableType = findType("Openable");
	if (openableType == null) { return "{\"ok\":false,\"error\":\"no-Openable\"}"; }
	for (var i = 0; i < wanted.Length; i++)
	{
		var candidate = wanted[i].Trim();
		if (candidate.Length == 0) { continue; }
		var prefab = UnityEngine.Resources.Load(candidate) as UnityEngine.GameObject;
		if (prefab == null) { tried.Append(candidate).Append(":none;"); continue; }
		var placed = UnityEngine.Object.Instantiate(prefab, spot, UnityEngine.Quaternion.identity) as UnityEngine.GameObject;
		if (placed == null) { tried.Append(candidate).Append(":no-instance;"); continue; }
		var openable = placed.GetComponent(openableType);
		if (openable == null) { tried.Append(candidate).Append(":no-openable;"); UnityEngine.Object.Destroy(placed); continue; }
		var building = placed.GetComponent("BuildingEntity");
		var health = -1f;
		if (building != null) { var property = building.GetType().GetProperty("health", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); if (property != null) { health = System.Convert.ToSingle(property.GetValue(building, null), inv); } }
		var here = placed.transform.position;
		return "{\"ok\":true"
			+ ",\"name\":\"" + placed.name.Replace("\"", "''") + "\""
			+ ",\"prefab\":\"" + candidate.Replace("\"", "''") + "\""
			+ ",\"x\":" + here.x.ToString("0.###", inv)
			+ ",\"y\":" + here.y.ToString("0.###", inv)
			+ ",\"health\":" + health.ToString("0.##", inv)
			+ ",\"tried\":\"" + tried.ToString() + "\"}";
	}
	return "{\"ok\":false,\"error\":\"no-prefab-loaded\",\"tried\":\"" + tried.ToString() + "\"}";
}))()
