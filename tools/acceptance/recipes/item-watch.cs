// recipe: item-watch
// args: tx=n ty=n dmg=n window=n
// serves: entity-destruction-drop-guest-fresh-state-loss, trap-destruction-drop-quantity-desync
// returns: ok, attempt, cellX, cellY, blockBefore, blockAfter, scanned, unidentified, items, error, detail
//
// Optionally breaks the cell (tx, ty) with the game's own DamageBlock when <dmg> is above zero, then
// polls up to <window> times, 100 ms apart, for an item NEW to this client within 45 units of the cell
// and returns the FIRST snapshot that carries one - so the self-destroying FreshItemDrop component
// (10 s) is read inside its window, on whichever client the run is judging. `new` means the item
// carries a READABLE, non-zero ItemInstanceId that this client's baseline does not hold: the game's
// generation-time world items carry no such component (CUO attaches it only to the runtime items it
// registers), so an item whose id is missing or zero is counted in `unidentified` and is never reported
// as new - otherwise a `dmg=0` observer would call an old ground item the drop it waited for.
// <window> is capped at 50: 50 x 100 ms of sleep plus one object scan per attempt must stay under the
// evaluator's 10 s ceiling (the driver clamps the eval timeout to min(10000, TimeoutMs)), or a slow
// window surfaces as a driver timeout instead of this recipe's own refusal. dmg=0 is the observer
// shape: poll only, break nothing. One eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\",\"detail\":\"no world is loaded\"}"; }
	var cell = new Vector2Int((int)({{n:tx}}), (int)({{n:ty}}));
	var dmg = (float)({{n:dmg}});
	var window = (int)({{n:window}});
	if (cell.x < 0 || cell.y < 0 || cell.x >= (int)world.width || cell.y >= (int)world.height) {
		return "{\"ok\":false,\"error\":\"out-of-range\",\"detail\":\"cell " + cell.x.ToString(inv) + "," + cell.y.ToString(inv) + " is outside the world\"}";
	}
	if (window < 1) { window = 1; }
	if (window > 50) { window = 50; }
	var center = world.BlockToWorldPos(cell);
	var before = world.GetBlock(cell);
	if (dmg > 0f) { world.DamageBlock(cell, dmg, true, false, false); }
	var after = world.GetBlock(cell);
	var baseline = new System.Collections.Generic.List<ulong>();
	var items0 = UnityEngine.Object.FindObjectsOfType<Item>(true);
	for (var i0 = 0; i0 < items0.Length; i0++) {
		var it0 = items0[i0];
		if (it0 == null || it0.GetComponentInParent<Body>() != null) { continue; }
		var p0 = it0.transform.position;
		if (p0.x < center.x - 45f || p0.x > center.x + 45f || p0.y < center.y - 45f || p0.y > center.y + 45f) { continue; }
		var comps0 = it0.GetComponents<UnityEngine.Component>();
		for (var c0 = 0; c0 < comps0.Length; c0++) {
			if (comps0[c0] == null || comps0[c0].GetType().Name != "ItemInstanceId") { continue; }
			var id0 = 0UL;
			var f0 = comps0[c0].GetType().GetField("Id", flags);
			if (f0 != null) { id0 = System.Convert.ToUInt64(f0.GetValue(comps0[c0])); }
			else {
				var pr0 = comps0[c0].GetType().GetProperty("Id", flags);
				if (pr0 != null) { id0 = System.Convert.ToUInt64(pr0.GetValue(comps0[c0], null)); }
			}
			if (id0 != 0UL) { baseline.Add(id0); }
			break;
		}
	}
	var last = "";
	var lastScanned = 0;
	var lastUnidentified = 0;
	for (var attempt = 0; attempt < window; attempt++) {
		System.Threading.Thread.Sleep(100);
		var items = UnityEngine.Object.FindObjectsOfType<Item>(true);
		var sb = new System.Text.StringBuilder();
		var emitted = 0;
		var scanned = 0;
		var unidentified = 0;
		var hasNew = false;
		for (var i = 0; i < items.Length; i++) {
			var it = items[i];
			if (it == null || it.GetComponentInParent<Body>() != null) { continue; }
			var p = it.transform.position;
			if (p.x < center.x - 45f || p.x > center.x + 45f || p.y < center.y - 45f || p.y > center.y + 45f) { continue; }
			scanned++;
			var itemId = 0UL;
			var hasId = false;
			var comps = it.GetComponents<UnityEngine.Component>();
			for (var c = 0; c < comps.Length; c++) {
				if (comps[c] == null || comps[c].GetType().Name != "ItemInstanceId") { continue; }
				var f = comps[c].GetType().GetField("Id", flags);
				if (f != null) { itemId = System.Convert.ToUInt64(f.GetValue(comps[c])); }
				else {
					var pr = comps[c].GetType().GetProperty("Id", flags);
					if (pr != null) { itemId = System.Convert.ToUInt64(pr.GetValue(comps[c], null)); }
				}
				break;
			}
			if (itemId != 0UL) { hasId = true; } else { unidentified++; }
			var isNew = hasId && !baseline.Contains(itemId);
			if (isNew) { hasNew = true; }
			var rb = it.GetComponent<UnityEngine.Rigidbody2D>();
			var vx = rb != null ? rb.velocity.x : 0f;
			var vy = rb != null ? rb.velocity.y : 0f;
			var av = rb != null ? rb.angularVelocity : 0f;
			var fresh = it.GetComponent("FreshItemDrop") != null;
			if (emitted > 0) { sb.Append(','); }
			emitted = emitted + 1;
			sb.Append("{\"id\":\"").Append(itemId.ToString(inv)).Append('"');
			sb.Append(",\"idReadable\":").Append(hasId ? "true" : "false");
			sb.Append(",\"type\":\"").Append(it.id).Append('"');
			sb.Append(",\"new\":").Append(isNew ? "true" : "false");
			sb.Append(",\"fresh\":").Append(fresh ? "true" : "false");
			sb.Append(",\"x\":").Append(p.x.ToString("0.###", inv)).Append(",\"y\":").Append(p.y.ToString("0.###", inv));
			sb.Append(",\"vx\":").Append(vx.ToString("0.###", inv)).Append(",\"vy\":").Append(vy.ToString("0.###", inv));
			sb.Append(",\"av\":").Append(av.ToString("0.###", inv)).Append('}');
		}
		last = sb.ToString();
		lastScanned = scanned;
		lastUnidentified = unidentified;
		if (hasNew) {
			return "{\"ok\":true,\"attempt\":" + attempt.ToString(inv)
				+ ",\"cellX\":" + cell.x.ToString(inv) + ",\"cellY\":" + cell.y.ToString(inv)
				+ ",\"blockBefore\":" + ((int)before).ToString(inv) + ",\"blockAfter\":" + ((int)after).ToString(inv)
				+ ",\"scanned\":" + scanned.ToString(inv) + ",\"unidentified\":" + unidentified.ToString(inv)
				+ ",\"items\":[" + sb.ToString() + "]}";
		}
	}
	return "{\"ok\":false,\"error\":\"no-new-item\",\"detail\":\"no new item with a readable id within "
		+ window.ToString(inv) + " x 100 ms; the last scan saw " + lastScanned.ToString(inv)
		+ " item(s), " + lastUnidentified.ToString(inv) + " without a readable id\""
		+ ",\"cellX\":" + cell.x.ToString(inv) + ",\"cellY\":" + cell.y.ToString(inv)
		+ ",\"blockBefore\":" + ((int)before).ToString(inv) + ",\"blockAfter\":" + ((int)after).ToString(inv)
		+ ",\"scanned\":" + lastScanned.ToString(inv) + ",\"unidentified\":" + lastUnidentified.ToString(inv)
		+ ",\"items\":[" + last + "]}";
}))()
