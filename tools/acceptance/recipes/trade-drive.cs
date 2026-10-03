// recipe: trade-drive
// args: mode=s index=n type=s
// serves: item-creation-registration-first row 7 (the trade regression: a purchase's item creation is registered)
// returns: ok, mode, index, type, traderCount, traderX, traderY, valueGiven, totalValueGiven, reputation, itemCount, buildHealth, traders, credited, valueBefore, valueAfter, totalBefore, totalAfter, priceExpected, stockBefore, stockAfter, stockRemoved, bought, boughtType, beforeCount, afterCount, products, reason, error, detail
//
// Drives the game's own trader entry on the NEAREST TraderScript:
//   mode=list  — the traders in this scene with their credit (valueGiven), reputation, stock and
//     build health (the purchase gate).
//   mode=give  — Utils.Create(<type>) handed to the trader (TraderScript.GiveItem): the native
//     credit path the trade domain reports. A refused give (no value, lifetime cap, contents) is
//     reported through credited=false, never as a silent ok.
//   mode=buy   — TraderScript.TryPurchase on the trader's stock entry <index>: the native purchase
//     creates the item on this side (Utils.Create + AutoPickUpItem) — the LOCAL creation half; the
//     run reads the host's table with container-read mode=host. The two silent refusal gates
//     (build.health < 200, valueGiven < price) are pre-read and reported through reason, so a
//     refusal is never confused with a purchase. <type> is used by mode=give only; the driver
//     substitutes every declared argument, so pass a placeholder when another mode ignores it.
// The id reader is a delegate over the const flags only: the evaluator's Mono REPL refuses a
// delegate that captures a local. One eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var mode = {{s:mode}};
	var index = (int)({{n:index}});
	var type = {{s:type}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var idOf = new System.Func<Item, ulong>(delegate(Item probe) {
		var components = probe.GetComponents<UnityEngine.Component>();
		for (var c = 0; c < components.Length; c++) {
			if (components[c] == null || components[c].GetType().Name != "ItemInstanceId") { continue; }
			var idField = components[c].GetType().GetField("Id", flags);
			if (idField != null) { return System.Convert.ToUInt64(idField.GetValue(components[c])); }
			var idProperty = components[c].GetType().GetProperty("Id", flags);
			if (idProperty != null) { return System.Convert.ToUInt64(idProperty.GetValue(components[c], null)); }
			return 0UL;
		}
		return 0UL;
	});
	var found = UnityEngine.Object.FindObjectsOfType(typeof(TraderScript));
	if (found == null || found.Length == 0) { return "{\"ok\":false,\"error\":\"no-trader\",\"detail\":\"no TraderScript is in this scene\"}"; }
	TraderScript trader = null;
	var best = 0f;
	for (var i = 0; i < found.Length; i++) {
		var candidate = found[i] as TraderScript;
		if (candidate == null) { continue; }
		var distance = UnityEngine.Vector2.Distance(candidate.transform.position, body.transform.position);
		if (trader == null || distance < best) { trader = candidate; best = distance; }
	}
	var stock = trader.items != null ? trader.items.Count : 0;
	var buildHealth = -1;
	var buildField = typeof(TraderScript).GetField("build", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
	if (buildField != null) {
		var buildValue = buildField.GetValue(trader);
		if (buildValue != null) {
			var healthField = buildValue.GetType().GetField("health", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			if (healthField != null) { buildHealth = System.Convert.ToInt32(healthField.GetValue(buildValue), inv); }
		}
	}
	var positionX = trader.transform.position.x.ToString("0.###", inv);
	var positionY = trader.transform.position.y.ToString("0.###", inv);
	if (mode == "list") {
		var sb = new System.Text.StringBuilder();
		var traders = 0;
		for (var i = 0; i < found.Length && i < 8; i++) {
			var candidate = found[i] as TraderScript;
			if (candidate == null) { continue; }
			if (traders > 0) { sb.Append(','); }
			traders = traders + 1;
			sb.Append("{\"character\":").Append(candidate.character.ToString(inv));
			sb.Append(",\"x\":").Append(candidate.transform.position.x.ToString("0.###", inv));
			sb.Append(",\"y\":").Append(candidate.transform.position.y.ToString("0.###", inv));
			sb.Append(",\"valueGiven\":").Append(candidate.valueGiven.ToString(inv));
			sb.Append(",\"totalValueGiven\":").Append(candidate.totalValueGiven.ToString(inv));
			sb.Append(",\"reputation\":").Append(candidate.reputation.ToString("0.###", inv));
			sb.Append(",\"hostility\":").Append(candidate.hostility.ToString("0.###", inv));
			sb.Append(",\"stock\":").Append((candidate.items != null ? candidate.items.Count : 0).ToString(inv));
			var candidateHealth = -1;
			var candidateBuildField = typeof(TraderScript).GetField("build", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			if (candidateBuildField != null) {
				var candidateBuild = candidateBuildField.GetValue(candidate);
				if (candidateBuild != null) {
					var candidateHealthField = candidateBuild.GetType().GetField("health", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					if (candidateHealthField != null) { candidateHealth = System.Convert.ToInt32(candidateHealthField.GetValue(candidateBuild), inv); }
				}
			}
			sb.Append(",\"buildHealth\":").Append(candidateHealth.ToString(inv));
			sb.Append(",\"items\":[");
			var items = candidate.items;
			if (items != null) {
				for (var s = 0; s < items.Count && s < 12; s++) {
					if (s > 0) { sb.Append(','); }
					sb.Append("{\"id\":\"").Append(items[s].id).Append('"');
					sb.Append(",\"value\":").Append(items[s].value.ToString(inv));
					sb.Append(",\"price\":").Append(candidate.ItemPrice(items[s]).ToString(inv));
					sb.Append(",\"preference\":").Append(((int)items[s].preference).ToString(inv));
					sb.Append(",\"bought\":").Append(items[s].bought ? "true" : "false");
					sb.Append('}');
				}
			}
			sb.Append("]}");
		}
		return "{\"ok\":true,\"mode\":\"list\",\"traderCount\":" + traders.ToString(inv)
			+ ",\"traderX\":" + positionX + ",\"traderY\":" + positionY
			+ ",\"buildHealth\":" + buildHealth.ToString(inv)
			+ ",\"valueGiven\":" + trader.valueGiven.ToString(inv)
			+ ",\"totalValueGiven\":" + trader.totalValueGiven.ToString(inv)
			+ ",\"reputation\":" + trader.reputation.ToString("0.###", inv)
			+ ",\"itemCount\":" + stock.ToString(inv)
			+ ",\"traders\":[" + sb.ToString() + "]}";
	}
	if (mode == "give") {
		var spawned = Utils.Create(type, body.transform.position, 0f);
		if (spawned == null) { return "{\"ok\":false,\"error\":\"create-failed\",\"detail\":\"Utils.Create returned nothing for " + type + "\"}"; }
		var given = spawned.GetComponent<Item>();
		if (given == null) {
			UnityEngine.Object.Destroy(spawned);
			return "{\"ok\":false,\"error\":\"create-failed\",\"detail\":\"the created object is not an Item\"}";
		}
		var valueBefore = trader.valueGiven;
		var totalBefore = trader.totalValueGiven;
		var giveDetail = "";
		try { trader.GiveItem(given); }
		catch (System.Exception e) { giveDetail = (e.GetType().Name + ": " + e.Message).Replace("\\", "/").Replace("\"", "'"); }
		if (giveDetail.Length != 0) { return "{\"ok\":false,\"error\":\"give-failed\",\"detail\":\"" + giveDetail + "\"}"; }
		var valueAfter = trader.valueGiven;
		var totalAfter = trader.totalValueGiven;
		var credited = valueAfter > valueBefore || totalAfter > totalBefore;
		return "{\"ok\":true,\"mode\":\"give\",\"type\":\"" + type + "\""
			+ ",\"traderX\":" + positionX + ",\"traderY\":" + positionY
			+ ",\"valueBefore\":" + valueBefore.ToString(inv)
			+ ",\"valueAfter\":" + valueAfter.ToString(inv)
			+ ",\"totalBefore\":" + totalBefore.ToString(inv)
			+ ",\"totalAfter\":" + totalAfter.ToString(inv)
			+ ",\"credited\":" + (credited ? "true" : "false")
			+ ",\"reason\":\"" + (credited ? "" : "the trader took no credit (value 0, lifetime cap reached, or the item still holds contents)") + "\""
			+ ",\"itemCount\":" + stock.ToString(inv) + "}";
	}
	if (mode != "buy") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be list, give or buy\"}"; }
	if (index < 0 || index >= stock) {
		return "{\"ok\":false,\"error\":\"bad-index\",\"detail\":\"index must name a stock entry (0.." + (stock - 1).ToString(inv) + ")\"}";
	}
	var entry = trader.items[index];
	var priceExpected = trader.ItemPrice(entry);
	var valueBeforeBuy = trader.valueGiven;
	var stockBefore = stock;
	var before = body.GetAllItemsThorough();
	var beforeCount = before.Count;
	var buyDetail = "";
	try { trader.TryPurchase(entry); }
	catch (System.Exception e) { buyDetail = (e.GetType().Name + ": " + e.Message).Replace("\\", "/").Replace("\"", "'"); }
	if (buyDetail.Length != 0) { return "{\"ok\":false,\"error\":\"buy-failed\",\"detail\":\"" + buyDetail + "\"}"; }
	var after = body.GetAllItemsThorough();
	var stockAfter = trader.items != null ? trader.items.Count : 0;
	var stockRemoved = stockAfter < stockBefore;
	var sbProducts = new System.Text.StringBuilder();
	var products = 0;
	for (var i = 0; i < after.Count; i++) {
		var carried = after[i];
		if (carried == null || before.Contains(carried)) { continue; }
		if (products > 0) { sbProducts.Append(','); }
		products = products + 1;
		sbProducts.Append("{\"id\":\"").Append(idOf(carried).ToString(inv)).Append('"');
		sbProducts.Append(",\"type\":\"").Append(carried.id).Append("\"}");
	}
	var bought = products > 0 || stockRemoved;
	var buyReason = bought
		? ""
		: (valueBeforeBuy < priceExpected
			? "insufficient-credit: the trader's credit is below the price (give items first)"
			: (buildHealth >= 0 && buildHealth < 200 ? "trader-unusable: build.health is below 200" : "refused without a stock change"));
	return "{\"ok\":true,\"mode\":\"buy\",\"index\":" + index.ToString(inv)
		+ ",\"type\":\"" + entry.id + "\""
		+ ",\"traderX\":" + positionX + ",\"traderY\":" + positionY
		+ ",\"valueGiven\":" + trader.valueGiven.ToString(inv)
		+ ",\"totalValueGiven\":" + trader.totalValueGiven.ToString(inv)
		+ ",\"reputation\":" + trader.reputation.ToString("0.###", inv)
		+ ",\"buildHealth\":" + buildHealth.ToString(inv)
		+ ",\"itemCount\":" + stockAfter.ToString(inv)
		+ ",\"boughtType\":\"" + entry.id + "\""
		+ ",\"priceExpected\":" + priceExpected.ToString(inv)
		+ ",\"valueBefore\":" + valueBeforeBuy.ToString(inv)
		+ ",\"stockBefore\":" + stockBefore.ToString(inv)
		+ ",\"stockAfter\":" + stockAfter.ToString(inv)
		+ ",\"stockRemoved\":" + (stockRemoved ? "true" : "false")
		+ ",\"beforeCount\":" + beforeCount.ToString(inv)
		+ ",\"afterCount\":" + after.Count.ToString(inv)
		+ ",\"bought\":" + (bought ? "true" : "false")
		+ ",\"reason\":\"" + buyReason + "\""
		+ ",\"products\":[" + sbProducts.ToString() + "]}";
}))()
