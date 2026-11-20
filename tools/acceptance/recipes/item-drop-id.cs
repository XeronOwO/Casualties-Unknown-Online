// recipe: item-drop-id
// args: instanceId=n
// returns: ok, dropped, instanceId, type, slot, error, detail
//
// Drops the local body's item whose CUO instance id is the given one, through the
// game's own Body.DropItem entry. The remote-backpack refresh row needs to remove a
// KNOWN item from the open window: item-provide's type selector cannot tell two
// copies of the same type apart, and the window's item is the one the host must lose.
((System.Func<string>)(() => {
	var idText = {{n:instanceId}};
	ulong wanted = 0UL;
	try { wanted = System.Convert.ToUInt64(idText); } catch { return "{\"ok\":false,\"error\":\"bad-instance-id\",\"detail\":\"" + idText + "\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var items = body.GetComponentsInChildren<Item>(true);
	for (var i = 0; i < items.Length; i++) {
		var candidate = items[i];
		if (candidate == null) { continue; }
		var parts = candidate.GetComponents<UnityEngine.Component>();
		for (var p = 0; p < parts.Length; p++) {
			if (parts[p] == null || parts[p].GetType().Name != "ItemInstanceId") { continue; }
			var idProperty = parts[p].GetType().GetProperty("Id");
			if (idProperty == null) { continue; }
			var id = System.Convert.ToUInt64(idProperty.GetValue(parts[p]));
			if (id != wanted) { continue; }
			var slot = candidate.transform.parent == null ? -1 : candidate.transform.parent.GetSiblingIndex();
			body.DropItem(candidate);
			return "{\"ok\":true,\"dropped\":" + (body.HoldingItem(candidate) ? "false" : "true")
				+ ",\"instanceId\":" + wanted.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"type\":\"" + candidate.id + "\",\"slot\":" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
		}
	}
	return "{\"ok\":false,\"error\":\"no-item\",\"instanceId\":" + wanted.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
