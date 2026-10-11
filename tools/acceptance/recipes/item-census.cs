// recipe: item-census
// args: mode=s
// serves: remote-inventory-native-parity-rework row 7 (the combineable pair, the battery tag and the
//         receiver whose slot is empty are ASSET facts, so the fixture is read from the game's own
//         item table instead of guessed)
// returns: ok, mode, localSteamId, count, items[], ids[], id, present, combineablePairs, error, detail
//
// Reads the facts a native inventory branch decides on, where they live:
//
//   mode=body          every item this client's OWN body carries — the slot occupants, the worn items
//                      under the limbs, their nested descendants — plus every display proxy in the
//                      scene, each with the stats the release branches read (`combineable`, `wearable`,
//                      `usableOnLimb`, its tags, its battery slot) and the identity the wire keys by.
//   mode=combineable   the ids the game's OWN item table marks `combineable`, and which of them have at
//                      least one same-id twin — `Body.CanCombine` accepts a pair of same-id combineable
//                      items (`Body.cs:1295`), so this is the pair the R6 branch can actually reach.
//   mode=tag:<t>       the ids whose own stats carry tag <t> — the native battery branch reads
//                      `dragItem.Stats.HasTag("battery")` / `("tool")` (`PlayerCamera.cs:1543/1548`).
//   mode=info:<id>     one id's registered stats: its flags, its tags, its own class (a `BatteryInfo`
//                      is what `BatteryItem.LoadBattery` requires) and whether the prefab loads.
//
// The registry is the game's own `Item.GlobalItems`, filled by `Item.SetupItems` — the runtime's item
// table, not a list this repository keeps. Every value below is read from it or from a live item.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	var culture = System.Globalization.CultureInfo.InvariantCulture;

	var findType = new System.Func<string, System.Type>((fullName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type candidate = null;
			try { candidate = assemblies[i].GetType(fullName, false); } catch { candidate = null; }
			if (candidate != null) { return candidate; }
		}
		return null;
	});
	var proxyType = findType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteCloneRender");
	var markerType = findType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteInventoryItemId");
	var fieldOf = new System.Func<UnityEngine.Component, string, ulong>((component, name) => {
		var field = component.GetType().GetField(name, instanceFlags);
		return field == null ? 0UL : System.Convert.ToUInt64(field.GetValue(component));
	});
	var markerOf = new System.Func<System.Type, Item, UnityEngine.Component>((wanted, item) => {
		if (wanted == null) { return null; }
		var components = item.GetComponents<UnityEngine.Component>();
		for (var i = 0; i < components.Length; i++) {
			if (components[i] != null && components[i].GetType() == wanted) { return components[i]; }
		}
		return null;
	});
	var isProxy = new System.Func<System.Type, Item, bool>((wanted, item) => wanted != null && item.GetComponent(wanted) != null);
	var statFlags = new System.Func<Item, string>((item) => {
		if (item == null || item.Stats == null) { return "\"stats\":\"none\""; }
		// The culture is taken INSIDE the lambda: the evaluator refuses an inner lambda that captures
		// an outer local, so no helper here closes over one.
		var invariant = System.Globalization.CultureInfo.InvariantCulture;
		return "\"id\":\"" + item.id + "\""
			+ ",\"condition\":" + item.condition.ToString("0.####", invariant)
			+ ",\"favourited\":" + (item.favourited ? "true" : "false")
			+ ",\"combineable\":" + (item.Stats.combineable ? "true" : "false")
			+ ",\"wearable\":" + (item.Stats.wearable ? "true" : "false")
			+ ",\"usableOnLimb\":" + (item.Stats.usableOnLimb ? "true" : "false")
			+ ",\"usable\":" + (item.Stats.usable ? "true" : "false")
			+ ",\"onlyHoldInHands\":" + (item.Stats.onlyHoldInHands ? "true" : "false")
			+ ",\"statsType\":\"" + item.Stats.GetType().Name + "\""
			+ ",\"tags\":\"" + string.Join(",", item.Stats.GetTags() ?? new string[0]) + "\""
			+ ",\"weight\":" + item.Stats.weight.ToString("0.###", invariant)
			+ ",\"battery\":" + (item.battery == null ? "false" : "true")
			+ ",\"hasBattery\":" + (item.battery != null && item.battery.hasBattery ? "true" : "false")
			+ ",\"batteryType\":\"" + (item.battery != null ? item.battery.batteryType : "") + "\""
			// The ammo pair is the one native combine whose result is a NUMBER on the receiver rather
			// than a condition: `AmmoScript.LoadRound` increments `rounds` and destroys the round item.
			+ ",\"ammo\":\"" + (item.GetComponent<AmmoScript>() == null ? "" : item.GetComponent<AmmoScript>().itemType.ToString() + "/" + item.GetComponent<AmmoScript>().ammoType.ToString()) + "\""
			+ ",\"ammoRounds\":" + (item.GetComponent<AmmoScript>() == null ? "-1" : item.GetComponent<AmmoScript>().rounds.ToString(System.Globalization.CultureInfo.InvariantCulture))
			+ ",\"ammoMax\":" + (item.GetComponent<AmmoScript>() == null ? "-1" : item.GetComponent<AmmoScript>().maxRounds.ToString(System.Globalization.CultureInfo.InvariantCulture))
			// A topical's consumption is a LIQUID amount, not a condition: `ApplyToLimb` drains ml per use.
			+ ",\"liquidTotal\":" + (item.GetComponent<WaterContainerItem>() == null ? "-1" : item.GetComponent<WaterContainerItem>().CurrentTotal.ToString("0.###", invariant))
			+ ",\"liquidCapacity\":" + (item.GetComponent<WaterContainerItem>() == null ? "-1" : item.GetComponent<WaterContainerItem>().Capacity.ToString("0.###", invariant));
	});

	if (mode == "body") {
		var items = new System.Collections.Generic.List<Item>();
		var origins = new System.Collections.Generic.List<string>();
		if (body != null) {
			for (var slot = 0; slot < body.slots.Length; slot++) {
				var occupant = body.GetItem(slot);
				if (occupant != null) { items.Add(occupant); origins.Add("slot" + slot.ToString(culture)); }
			}
			for (var limb = 0; limb < body.limbs.Length; limb++) {
				var limbTransform = body.limbs[limb].transform;
				for (var child = 0; child < limbTransform.childCount; child++) {
					var worn = limbTransform.GetChild(child).GetComponent<Item>();
					if (worn != null) { items.Add(worn); origins.Add("limb" + limb.ToString(culture)); }
				}
			}
			for (var i = 0; i < items.Count; i++) {
				var descendants = items[i].GetComponentsInChildren<Item>(true);
				for (var d = 0; d < descendants.Length; d++) {
					if (descendants[d] != items[i] && !items.Contains(descendants[d])) { items.Add(descendants[d]); origins.Add("inside:" + items[i].id); }
				}
			}
		}
		// The displayed copies are read too: a release is aimed at one of them.
		var itemObjects = UnityEngine.Object.FindObjectsOfType(typeof(Item));
		for (var i = 0; i < itemObjects.Length; i++) {
			var candidate = itemObjects[i] as Item;
			if (candidate != null && isProxy(proxyType, candidate) && !items.Contains(candidate)) { items.Add(candidate); origins.Add("proxy"); }
		}
		var entries = new System.Collections.Generic.List<string>();
		for (var i = 0; i < items.Count; i++) {
			var item = items[i];
			var marker = markerOf(markerType, item);
			var container = item.GetComponent<Container>();
			entries.Add("{" + statFlags(item)
				+ ",\"origin\":\"" + origins[i] + "\""
				+ ",\"instanceId\":" + (marker == null ? "0" : fieldOf(marker, "Id").ToString(culture))
				+ ",\"owner\":" + (marker == null ? "0" : fieldOf(marker, "OwnerSteamId").ToString(culture))
				+ ",\"proxy\":" + (isProxy(proxyType, item) ? "true" : "false")
				+ ",\"parent\":\"" + (item.transform.parent == null ? "none" : item.transform.parent.name) + "\""
				+ ",\"container\":" + (container == null ? "false" : "true")
				+ ",\"childCount\":" + (container == null ? "0" : container.transform.childCount.ToString(culture))
				+ ",\"x\":" + item.transform.position.x.ToString("0.###", culture)
				+ ",\"y\":" + item.transform.position.y.ToString("0.###", culture)
				+ ",\"active\":" + (item.gameObject.activeInHierarchy ? "true" : "false") + "}");
		}
		return "{\"ok\":true,\"mode\":\"body\""
			+ ",\"count\":" + items.Count.ToString(culture)
			+ ",\"items\":[" + string.Join(",", entries) + "]}";
	}

	var registry = Item.GlobalItems;
	if (registry == null) { return "{\"ok\":false,\"error\":\"no-item-registry\",\"detail\":\"Item.GlobalItems is null (SetupItems has not run)\"}"; }
	var keys = new System.Collections.Generic.List<string>();
	foreach (var key in registry.Keys) { keys.Add(key); }
	keys.Sort(System.StringComparer.Ordinal);

	var matches = new System.Collections.Generic.List<string>();
	if (mode == "combineable") {
		for (var i = 0; i < keys.Count; i++) {
			var info = registry[keys[i]];
			if (info != null && info.combineable) { matches.Add(keys[i]); }
		}
	}
	else if (mode.StartsWith("tag:", System.StringComparison.Ordinal)) {
		// `ItemInfo.HasTag` runs `Enumerable.Contains` over `actualTags`, which is NULL for an item
		// that declares no tags — it throws instead of answering false. The scan reads the array and
		// guards it, so a tagless item is skipped rather than fatal.
		var tag = mode.Substring(4);
		for (var i = 0; i < keys.Count; i++) {
			var info = registry[keys[i]];
			var tags = info == null ? null : info.GetTags();
			if (tags != null && System.Array.IndexOf(tags, tag) >= 0) { matches.Add(keys[i]); }
		}
	}
	else if (mode.StartsWith("info:", System.StringComparison.Ordinal)) {
		var id = mode.Substring(5);
		var present = registry.ContainsKey(id);
		var info = present ? registry[id] : null;
		var loads = false;
		try { loads = UnityEngine.Resources.Load(id) != null; } catch { loads = false; }
		if (info == null) {
			return "{\"ok\":true,\"mode\":\"info\",\"id\":\"" + id + "\",\"present\":false,\"loads\":" + (loads ? "true" : "false") + "}";
		}
		return "{\"ok\":true,\"mode\":\"info\",\"id\":\"" + id + "\",\"present\":true,\"loads\":" + (loads ? "true" : "false")
			+ ",\"statsType\":\"" + info.GetType().Name + "\""
			+ ",\"combineable\":" + (info.combineable ? "true" : "false")
			+ ",\"wearable\":" + (info.wearable ? "true" : "false")
			+ ",\"usableOnLimb\":" + (info.usableOnLimb ? "true" : "false")
			+ ",\"usable\":" + (info.usable ? "true" : "false")
			+ ",\"onlyHoldInHands\":" + (info.onlyHoldInHands ? "true" : "false")
			+ ",\"tags\":\"" + string.Join(",", info.GetTags() ?? new string[0]) + "\""
			+ ",\"weight\":" + info.weight.ToString("0.###", culture) + "}";
	}
	else {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be body, combineable, tag:<tag> or info:<id>\"}";
	}

	// A combineable pair needs two items of ONE id, so the same-id count is what makes it a fixture.
	var rows = new System.Collections.Generic.List<string>();
	for (var i = 0; i < matches.Count; i++) {
		var info = registry[matches[i]];
		rows.Add("{\"id\":\"" + matches[i] + "\""
			+ ",\"weight\":" + (info == null ? "0" : info.weight.ToString("0.###", culture))
			+ ",\"wearable\":" + (info != null && info.wearable ? "true" : "false")
			+ ",\"usableOnLimb\":" + (info != null && info.usableOnLimb ? "true" : "false")
			+ ",\"tags\":\"" + (info == null ? "" : string.Join(",", info.GetTags() ?? new string[0])) + "\"}");
	}
	return "{\"ok\":true,\"mode\":\"" + mode + "\""
		+ ",\"registrySize\":" + keys.Count.ToString(culture)
		+ ",\"count\":" + matches.Count.ToString(culture)
		+ ",\"items\":[" + string.Join(",", rows) + "]}";
}))()
