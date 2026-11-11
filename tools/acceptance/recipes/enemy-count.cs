// recipe: enemy-count
// args: none
// serves: 1.0-regression-sweep enemy rows (a client's animal count without the per-animal array)
// returns: ok, count, bodyX, bodyY, error
//
// The count-only twin of `enemy-scene`: a member that holds several hundred animals overflows the
// driver's frame when every animal's fields are answered, so this recipe answers the number alone.
// The same filter as `enemy-scene` (live `BuildingEntity` rows with `animal` set). Read-only.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var bx = body.transform.position.x;
	var by = body.transform.position.y;
	var animals = UnityEngine.Object.FindObjectsOfType<BuildingEntity>(true);
	var count = 0;
	for (var i = 0; i < animals.Length; i++) {
		var e = animals[i];
		if (e == null || !e.animal) { continue; }
		count = count + 1;
	}
	return "{\"ok\":true,\"count\":" + count.ToString(inv)
		+ ",\"bodyX\":" + bx.ToString("R", inv) + ",\"bodyY\":" + by.ToString("R", inv) + "}";
}))()

