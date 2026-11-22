// recipe: usable-hover
// args: none
// returns: ok, name, layer, tag, building, usable, parentNames, parentBuilding
//
// Answers why a click on an object does nothing: the game fills `usableObject` in `PlayerCamera.HandleWorldHover`
// (`PlayerCamera.cs:2184-2217`) with `Physics2D.OverlapPoint(pointerWorld, Item|Ground|Descriptor|Limb)` and then requires
// `collider.TryGetComponent<BuildingEntity>(out building)` **on that same collider's GameObject** (`:2211-2215`); the usable
// object itself is taken from the building (`:2255`). A collider whose `BuildingEntity` lives on a parent turns every click
// into a miss, and the only visible symptom is "the door did nothing".
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var cam = Camera.main;
	if (cam == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var point = cam.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
	var mask = UnityEngine.LayerMask.GetMask(new[] { "Item", "Ground", "Descriptor", "Limb" });
	var hit = UnityEngine.Physics2D.OverlapPoint(point, mask);
	if (hit == null) { hit = UnityEngine.Physics2D.OverlapPoint(point, UnityEngine.LayerMask.GetMask(new[] { "Body" })); }
	if (hit == null) { return "{\"ok\":false,\"error\":\"nothing-under-the-pointer\"}"; }
	var building = hit.gameObject.GetComponent("BuildingEntity");
	var usable = hit.gameObject.GetComponent("UsableObject");
	var parents = new System.Text.StringBuilder();
	var parentBuilding = "";
	var top = hit.transform.parent;
	for (var depth = 0; depth < 4 && top != null; depth++)
	{
		parents.Append(top.name).Append(" > ");
		if (top.GetComponent("BuildingEntity") != null) { parentBuilding = top.name; }
		if (top.GetComponent("UsableObject") != null) { usable = top.GetComponent("UsableObject"); }
		top = top.parent;
	}
	return "{\"ok\":true"
		+ ",\"name\":\"" + hit.gameObject.name.Replace("\"", "''") + "\""
		+ ",\"layer\":\"" + UnityEngine.LayerMask.LayerToName(hit.gameObject.layer) + "\""
		+ ",\"tag\":\"" + hit.gameObject.tag + "\""
		+ ",\"building\":" + (building != null ? "true" : "false")
		+ ",\"usable\":" + (usable != null ? "true" : "false")
		+ ",\"parentNames\":\"" + parents.ToString() + "\""
		+ ",\"parentBuilding\":\"" + parentBuilding + "\"}";
}))()
