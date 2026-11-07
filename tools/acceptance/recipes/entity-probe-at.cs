// recipe: entity-probe-at
// args: x=n y=n
// returns: ok, x, y, found, health, prefab, error
//
// Reads the live BuildingEntity at an exact world point through the same `Physics2D.OverlapPoint` the restore's
// building-entity appliers use. `found=false` means the regenerated layer has no entity there; `health` is the
// entity's own field. Read-only.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = (float){{n:x}};
	var y = (float){{n:y}};
	var hit = UnityEngine.Physics2D.OverlapPoint(new UnityEngine.Vector2(x, y));
	var entity = hit != null ? hit.GetComponent<BuildingEntity>() : null;
	if (entity == null) {
		return "{\"ok\":true,\"x\":" + x.ToString("0.##", inv) + ",\"y\":" + y.ToString("0.##", inv) + ",\"found\":false}";
	}
	return "{\"ok\":true,\"x\":" + x.ToString("0.##", inv) + ",\"y\":" + y.ToString("0.##", inv)
		+ ",\"found\":true,\"health\":" + entity.health.ToString("0.##", inv)
		+ ",\"prefab\":\"" + (entity.id == null ? "" : entity.id) + "\"}";
}))()
