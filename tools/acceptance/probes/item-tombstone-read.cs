// recipe: item-tombstone-read
// args: id=s
// serves: item-creation-registration-first rows 2 and 5 (the refused-creation tombstone exists, and the session end forgets it)
// returns: ok, count, id, remembered, reason, error, detail
//
// Reads the host's RefusedItemCreations table directly: how many tombstones it holds, and whether
// the given id is one of them. Row 2's later operation must be answered with the precise reason
// while the tombstone is held; row 5's session end must leave the table empty, so the same id's
// later operation is the plain unjudged refusal instead. Read-only. One eval.
((System.Func<string>)(() => {
	var rawId = {{s:id}};
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var refused = services.GetService(typeof(CasualtiesUnknownOnline.Application.Kernel.RefusedItemCreations)) as CasualtiesUnknownOnline.Application.Kernel.RefusedItemCreations;
	if (refused == null) { return "{\"ok\":false,\"error\":\"no-refused-table\"}"; }
	var id = 0UL;
	if (rawId.Length != 0) { ulong.TryParse(rawId, System.Globalization.NumberStyles.None, inv, out id); }
	var remembered = false;
	var reason = "";
	if (id != 0UL) {
		var value = CasualtiesUnknownOnline.GameState.RejectionReason.UnknownAggregate;
		remembered = refused.TryGet(id, out value);
		if (remembered) { reason = value.ToString(); }
	}
	return "{\"ok\":true,\"count\":" + refused.Count.ToString(inv)
		+ ",\"id\":\"" + id.ToString(inv) + "\""
		+ ",\"remembered\":" + (remembered ? "true" : "false")
		+ ",\"reason\":\"" + reason + "\"}";
}))()
