// recipe: lockpick-open
// args: none
// returns: ok, openables, distance, entityX, entityY, prefab, bodyX, bodyY, minigame, error, detail
//
// Starts the game's own lockpick minigame the way its interaction does: it finds the nearest `Openable`, stands the body beside it, and calls
// `Openable.OnUse()` — the exact method the player's interact runs (`reversing/Assembly-CSharp/Assembly-CSharp/Openable.cs`). That method resolves the
// body's `lockpickingkit` and starts `LockpingMinigame` on the building entity; the batch is expected to have given the kit first (`/give @s
// lockpickingkit`) and to hold INT >= 10, because the same method nulls the item below that.
//
// Why a fixture and not a click: the row it serves (`sync-player-pain-vocalizations-and-bark`) is about the `gore2` the minigame plays when the pick is
// held off the lock, and reaching the minigame through the player's own interact needs the entity on screen and a real click. This calls the same entry
// the click reaches, and reports whether a minigame actually started.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }

	var openables = UnityEngine.Object.FindObjectsOfType<Openable>();
	if (openables.Length == 0) { return "{\"ok\":false,\"error\":\"no-openable\",\"openables\":0}"; }

	var best = (Openable)null;
	var bestDistance = float.MaxValue;
	for (var i = 0; i < openables.Length; i++) {
		var candidate = openables[i];
		if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.isKeypad || candidate.instantOpen) { continue; }
		var distance = UnityEngine.Vector2.Distance(candidate.transform.position, body.transform.position);
		if (distance < bestDistance) { best = candidate; bestDistance = distance; }
	}
	if (best == null) { return "{\"ok\":false,\"error\":\"no-live-openable\",\"openables\":" + openables.Length.ToString(inv) + "}"; }

	body.transform.position = best.transform.position + new UnityEngine.Vector3(1.2f, 0f, 0f);
	best.OnUse();

	var before = MinigameBase.main == null ? "none" : (MinigameBase.main.currentMinigame == null ? "none" : MinigameBase.main.currentMinigame.GetType().Name);
	return "{\"ok\":true,\"openables\":" + openables.Length.ToString(inv)
		+ ",\"distance\":" + bestDistance.ToString("0.#", inv)
		+ ",\"entityX\":" + best.transform.position.x.ToString("0.#", inv)
		+ ",\"entityY\":" + best.transform.position.y.ToString("0.#", inv)
		+ ",\"prefab\":\"" + (best.GetComponent<BuildingEntity>().id ?? "") + "\""
		+ ",\"bodyX\":" + body.transform.position.x.ToString("0.#", inv)
		+ ",\"bodyY\":" + body.transform.position.y.ToString("0.#", inv)
		+ ",\"minigame\":\"" + before + "\"}";
}))()
