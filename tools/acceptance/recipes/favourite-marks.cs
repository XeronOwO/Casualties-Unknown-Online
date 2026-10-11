// recipe: favourite-marks
// args: mode=s instance=s owner=s item=s
// serves: favourite-marks-are-local-and-saved (rows 2, 3, 4 and 7 read the live mark set, and the
//         batch's declared fixture path writes one when the physical cursor cannot be placed)
// returns: ok, mode, count, marks[], instanceId, owner, itemId, toggled, countAfter, marksAfter[], error, detail
//
// Reads — and in mode=toggle writes — the LOCAL player's own favourite-mark ledger through the one holder
// the display seams use. The ledger itself is the runtime's (`LocalFavouriteMarks`, internal) and is
// reached through `CasualtiesUnknownOnline.GameAdapter.LocalFavouriteMarkStore.Current`, so this recipe
// reflects on both rather than adding a production seam for a probe.
//
//   mode=read     the marks in force right now: how many, and each one's (instance id, owner, definition).
//   mode=toggle   the batch's declared FIXTURE path: `LocalFavouriteMarkStore.Current.Toggle(instance,
//                 owner, item)` — the same call the native gesture's capture makes — plus the set it
//                 produced. Only a run that records the substitution may use it; the ticket's acceptance
//                 names the gesture as the real path.
//
// A mark is a local, personal act: nothing here writes an item, and nothing reaches the wire.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags staticFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var instanceArg = {{s:instance}};
	var ownerArg = {{s:owner}};
	var itemArg = {{s:item}};
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
	var storeType = findType("CasualtiesUnknownOnline.GameAdapter.LocalFavouriteMarkStore");
	if (storeType == null) { return "{\"ok\":false,\"error\":\"no-store\",\"detail\":\"the plugin's LocalFavouriteMarkStore type is not loaded\"}"; }
	var currentProperty = storeType.GetProperty("Current", staticFlags);
	var ledger = currentProperty == null ? null : currentProperty.GetValue(null);
	if (ledger == null) { return "{\"ok\":false,\"error\":\"no-ledger\",\"detail\":\"LocalFavouriteMarkStore.Current answered null\"}"; }
	var ledgerType = ledger.GetType();
	// The evaluator's REPL refuses a nested lambda that captures an outer local, so everything these two
	// need arrives as a parameter (the binding flags are const, which the compiler inlines).
	var listMarks = new System.Func<object, string>((target) => {
		var invariant = System.Globalization.CultureInfo.InvariantCulture;
		var snapshot = target.GetType().GetMethod("Snapshot", instanceFlags).Invoke(target, null);
		var entries = new System.Collections.Generic.List<string>();
		foreach (var mark in (System.Collections.IEnumerable)snapshot) {
			var markType = mark.GetType();
			entries.Add("{\"instanceId\":" + System.Convert.ToUInt64(markType.GetProperty("ItemInstanceId", instanceFlags).GetValue(mark)).ToString(invariant)
				+ ",\"owner\":" + System.Convert.ToUInt64(markType.GetProperty("OwnerSteamId", instanceFlags).GetValue(mark)).ToString(invariant)
				+ ",\"itemId\":\"" + System.Convert.ToString(markType.GetProperty("ItemId", instanceFlags).GetValue(mark)) + "\"}");
		}
		return "[" + string.Join(",", entries) + "]";
	});
	var countOf = new System.Func<object, int>((target) => {
		var property = target.GetType().GetProperty("Count", instanceFlags);
		return property == null ? -1 : System.Convert.ToInt32(property.GetValue(target));
	});
	var before = listMarks(ledger);
	var count = countOf(ledger);
	if (mode == "read") {
		return "{\"ok\":true,\"mode\":\"read\",\"count\":" + count.ToString(culture) + ",\"marks\":" + before + "}";
	}
	if (mode == "toggle") {
		var instanceId = 0UL;
		if (!ulong.TryParse(instanceArg, out instanceId) || instanceId == 0) { return "{\"ok\":false,\"error\":\"bad-instance\",\"detail\":\"instance must be a non-zero item instance id\"}"; }
		var ownerId = 0UL;
		if (!ulong.TryParse(ownerArg, out ownerId) || ownerId == 0) { return "{\"ok\":false,\"error\":\"bad-owner\",\"detail\":\"owner must be a non-zero SteamId\"}"; }
		if (itemArg.Length == 0) { return "{\"ok\":false,\"error\":\"bad-item\",\"detail\":\"item must be the definition id the mark records\"}"; }
		var toggleMethod = ledgerType.GetMethod("Toggle", instanceFlags);
		if (toggleMethod == null) { return "{\"ok\":false,\"error\":\"no-toggle\",\"detail\":\"the ledger carries no Toggle method\"}"; }
		var toggled = (bool)toggleMethod.Invoke(ledger, new object[] { instanceId, ownerId, itemArg });
		return "{\"ok\":true,\"mode\":\"toggle\""
			+ ",\"count\":" + count.ToString(culture) + ",\"marks\":" + before
			+ ",\"instanceId\":" + instanceId.ToString(culture)
			+ ",\"owner\":" + ownerId.ToString(culture)
			+ ",\"itemId\":\"" + itemArg + "\""
			+ ",\"toggled\":" + (toggled ? "true" : "false")
			+ ",\"countAfter\":" + countOf(ledger).ToString(culture)
			+ ",\"marksAfter\":" + listMarks(ledger) + "}";
	}
	return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be read or toggle\"}";
}))()
