// recipe: item-destroy
// args: id=s
// serves: guest-command-loss-reconciliation rows 3 and 4 (a guest's own destroy whose report is swallowed)
// returns: ok, id, type, destroyed, error, detail
//
// Destroys one RUNTIME world item — a world item carrying a readable ItemInstanceId and held by no body —
// by calling UnityEngine.Object.Destroy on its GameObject, so the game's own Item.OnDestroy hook runs and
// the adapter commits the ItemDestroy report through the same path a native decay-to-zero or a consumed
// item takes (ItemPatches.ItemOnDestroyPatch -> ItemWorldSync.OnItemDestroyed -> SendItemDestroyed). The
// destroy lands at the end of the frame, so the report is read from the log, not from this call's result.
// One eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var rawId = {{s:id}};
	var wantedId = 0UL;
	if (!ulong.TryParse(rawId, System.Globalization.NumberStyles.None, inv, out wantedId) || wantedId == 0UL) {
		return "{\"ok\":false,\"error\":\"bad-id\",\"detail\":\"id must be a non-zero decimal item instance id\"}";
	}
	var found = UnityEngine.Object.FindObjectsOfType(typeof(Item));
	Item target = null;
	for (var i = 0; i < found.Length; i++) {
		var candidate = found[i] as Item;
		if (candidate == null || candidate.GetComponentInParent<Body>() != null) { continue; }
		var components = candidate.GetComponents<UnityEngine.Component>();
		for (var c = 0; c < components.Length; c++) {
			if (components[c] == null || components[c].GetType().Name != "ItemInstanceId") { continue; }
			var candidateId = 0UL;
			var idField = components[c].GetType().GetField("Id", flags);
			if (idField != null) { candidateId = System.Convert.ToUInt64(idField.GetValue(components[c])); }
			else {
				var idProperty = components[c].GetType().GetProperty("Id", flags);
				if (idProperty != null) { candidateId = System.Convert.ToUInt64(idProperty.GetValue(components[c], null)); }
			}
			if (candidateId == wantedId) { target = candidate; }
			break;
		}
		if (target != null) { break; }
	}
	if (target == null) {
		return "{\"ok\":false,\"error\":\"no-world-item\",\"detail\":\"no world item carries id " + wantedId.ToString(inv) + "\"}";
	}
	var type = target.id;
	var position = target.transform.position;
	UnityEngine.Object.Destroy(target.gameObject);
	return "{\"ok\":true,\"id\":\"" + wantedId.ToString(inv) + "\",\"type\":\"" + type + "\",\"destroyed\":true"
		+ ",\"x\":" + position.x.ToString("0.###", inv) + ",\"y\":" + position.y.ToString("0.###", inv) + "}";
}))()
