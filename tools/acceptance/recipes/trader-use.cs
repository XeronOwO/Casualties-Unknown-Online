// recipe: trader-use
// args: none
// serves: the trader row's entry — opening the trade screen through the same call the game's own interact
//         path ends in (TraderScript.OnUse), on the nearest trader that stocks something
// returns: ok, menuOpen, menuBefore, name, x, y, distance, itemCount, cheapestIndex, cheapestPrice, valueGiven, detail
//
// The interact key is a pointer path: the game routes it through HandleDragging -> the object under the
// cursor -> the object's own use call, and this row measured that path failing at a trader standing three
// units away with the pointer aimed at its transform (menuOpen stayed false for five seconds). The call
// below is the one that path ends in, so the screen and everything after it are still the game's own; the
// pointer half is recorded as the row's residual in the ticket rather than pretended to work.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var all = UnityEngine.Object.FindObjectsOfType(typeof(TraderScript));
	TraderScript nearest = null;
	var best = -1.0f;
	for (var i = 0; i < all.Length; i++) {
		var candidate = all[i] as TraderScript;
		if (candidate == null || candidate.items == null || candidate.items.Count == 0) { continue; }
		var far = UnityEngine.Vector2.Distance(candidate.transform.position, body.transform.position);
		if (nearest == null || far < best) { nearest = candidate; best = far; }
	}
	if (nearest == null) { return "{\"ok\":false,\"error\":\"no-trader\"}"; }
	var menuBefore = (PlayerCamera.main.tradeMenu != null && PlayerCamera.main.tradeMenu.activeSelf) ? "true" : "false";
	// The game opens its trade screen only when the camera already holds a trader
	// (PlayerCamera.ToggleTradeMenu returns while `currentTrader` is null), and the pointer path that sets that
	// field reads the native cursor, which this driver cannot move (measured: the drag path entered with
	// usable=false at a trader three units away). The field below is the one that path sets; everything after it
	// is the game's own.
	PlayerCamera.main.currentTrader = nearest;
	nearest.OnUse();
	var menuOpen = (PlayerCamera.main.tradeMenu != null && PlayerCamera.main.tradeMenu.activeSelf) ? "true" : "false";
	var cheapestIndex = -1;
	var cheapestPrice = -1;
	for (var p = 0; p < nearest.items.Count; p++) {
		var priced = nearest.ItemPrice(nearest.items[p]);
		if (priced <= 0) { continue; }
		if (cheapestIndex < 0 || priced < cheapestPrice) { cheapestIndex = p; cheapestPrice = priced; }
	}
	return "{\"ok\":true,\"menuBefore\":" + menuBefore + ",\"menuOpen\":" + menuOpen
		+ ",\"name\":\"" + nearest.name.Replace("\"", "'") + "\""
		+ ",\"x\":" + nearest.transform.position.x.ToString("0.###", inv)
		+ ",\"y\":" + nearest.transform.position.y.ToString("0.###", inv)
		+ ",\"distance\":" + best.ToString("0.###", inv)
		+ ",\"itemCount\":" + nearest.items.Count.ToString(inv)
		+ ",\"cheapestIndex\":" + cheapestIndex.ToString(inv)
		+ ",\"cheapestPrice\":" + cheapestPrice.ToString(inv)
		+ ",\"valueGiven\":" + nearest.valueGiven.ToString(inv) + "}";
}))()

