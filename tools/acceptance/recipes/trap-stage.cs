// recipe: trap-stage
// args: x=n y=n dx=n
// serves: host-blast-rolls-a-second-drop-set (its two acceptance rows need a mine beside a droppable building)
// returns: ok, x, y, mineX, mineY, crateX, crateY, mineFound, crateFound, error, detail
//
// Places the two entities the blast rows need, from the GAME'S OWN prefabs: a `landmine` (the destructive carrier a
// member can report) and a `containercrate` (a droppable building next to it). `dx` is the horizontal distance between
// them; the caller passes the host body's own position, which the world generator treats as a floor reference.
//
// The placement is the run's STAGING, not a gameplay entry: no CUO control places a mine, which is exactly why the
// ticket's rows could not be read on a layer that generated none.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = {{n:x}};
	var y = {{n:y}};
	var dx = {{n:dx}};
	var minePrefab = UnityEngine.Resources.Load("landmine") as UnityEngine.GameObject;
	var cratePrefab = UnityEngine.Resources.Load("containercrate") as UnityEngine.GameObject;
	if (minePrefab == null || cratePrefab == null) { return "{\"ok\":false,\"error\":\"no-prefab\",\"detail\":\"landmine or containercrate missing from Resources\"}"; }
	var minePos = new UnityEngine.Vector2(x, y + 0.5f);
	var cratePos = new UnityEngine.Vector2(x + dx, y + 0.5f);
	UnityEngine.Object.Instantiate(minePrefab, minePos, UnityEngine.Quaternion.identity);
	UnityEngine.Object.Instantiate(cratePrefab, cratePos, UnityEngine.Quaternion.identity);
	return "{\"ok\":true"
		+ ",\"x\":" + x.ToString("0.##", inv)
		+ ",\"y\":" + y.ToString("0.##", inv)
		+ ",\"mineX\":" + minePos.x.ToString("0.##", inv)
		+ ",\"mineY\":" + minePos.y.ToString("0.##", inv)
		+ ",\"crateX\":" + cratePos.x.ToString("0.##", inv)
		+ ",\"crateY\":" + cratePos.y.ToString("0.##", inv)
		+ ",\"mineFound\":true,\"crateFound\":true}";
}))()
