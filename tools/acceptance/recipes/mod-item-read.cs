// recipe: mod-item-read
// args: id=s
// serves: mod-item-declaration-parity rows 1-3 (the declared recognition gate each client materialized, and
//         the game's own presentation of a live instance of that definition)
// returns: ok, id, present, recognitionMin, bodyInt, recognizable, instanceFound, hoverName, hoverDescription, error, detail
//
// Reads one item definition out of the game's OWN table (`Item.GlobalItems`) and the game's own answer for
// it on THIS client: the recognition gate the definition carries (`ItemInfo.rec.min`), this body's
// `skills.INT`, the gate's own verdict (`Recognition.recognizable`, which is `min <= INT`), and — when a
// live instance of that definition exists in the world — the two strings
// `PlayerCamera.ItemHoverDescription` returns for it, which is the game's own method rather than a
// re-implementation of its branch (below the gate it answers `unknownobject` and appends the warning line
// that prints the gate). Read-only: it creates nothing and writes nothing, so a run may read it on every
// client. No inner delegate captures a local: the evaluator's Mono REPL refuses that shape. One eval.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var id = {{s:id}};
	ItemInfo info;
	if (!Item.GlobalItems.TryGetValue(id, out info) || info == null) {
		return "{\"ok\":true,\"id\":\"" + id + "\",\"present\":false}";
	}
	var min = info.rec != null ? info.rec.min : -1;
	var camera = PlayerCamera.main;
	var body = camera != null ? camera.body : null;
	var intel = body != null ? body.skills.INT : -1;
	var recognizable = info.rec != null && body != null && info.rec.recognizable;
	var instanceFound = false;
	var hoverName = "";
	var hoverDescription = "";
	var found = UnityEngine.Object.FindObjectsOfType(typeof(Item));
	for (var i = 0; i < found.Length; i++) {
		var item = found[i] as Item;
		if (item == null || item.id != id) { continue; }
		instanceFound = true;
		var hover = PlayerCamera.ItemHoverDescription(item);
		hoverName = (hover.Item1 ?? "").Replace("\\", "/").Replace("\"", "'").Replace("\r", " ").Replace("\n", " ");
		hoverDescription = (hover.Item2 ?? "").Replace("\\", "/").Replace("\"", "'").Replace("\r", " ").Replace("\n", " | ");
		break;
	}
	return "{\"ok\":true,\"id\":\"" + id + "\""
		+ ",\"present\":true"
		+ ",\"recognitionMin\":" + min.ToString(inv)
		+ ",\"bodyInt\":" + intel.ToString(inv)
		+ ",\"recognizable\":" + (recognizable ? "true" : "false")
		+ ",\"instanceFound\":" + (instanceFound ? "true" : "false")
		+ ",\"hoverName\":\"" + hoverName + "\""
		+ ",\"hoverDescription\":\"" + hoverDescription + "\"}";
}))()
