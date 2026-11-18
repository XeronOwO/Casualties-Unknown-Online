// recipe: bag-instance
// args: type=s
// returns: ok, type, slot, instanceId, error, detail
//
// Answers the CUO instance id of the first body-slot item of a given prefab type, from the item's
// own `ItemInstanceId` component (reached by name, like the give probe, because the type is
// internal to the adapter). The give row needs it to name a CONTAINER destination on the wire:
// `body-items` lists `slot:prefab` only, and the give intent carries the container's instance id.
((System.Func<string>)(() => {
	var wanted = {{s:type}};
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var body = camera.body;
	if (body.slots == null) { return "{\"ok\":false,\"error\":\"no-slots\"}"; }

	var idOf = new System.Func<Item, ulong>((candidate) => {
		var parts = candidate.GetComponents<UnityEngine.Component>();
		for (var p = 0; p < parts.Length; p++)
		{
			if (parts[p] == null || parts[p].GetType().Name != "ItemInstanceId") { continue; }
			var idProperty = parts[p].GetType().GetProperty("Id");
			if (idProperty != null) { return System.Convert.ToUInt64(idProperty.GetValue(parts[p])); }
		}
		return 0UL;
	});

	for (var i = 0; i < body.slots.Length; i++)
	{
		var item = body.GetItem(i);
		if (item == null) { continue; }
		if (!string.IsNullOrEmpty(wanted) && wanted != "any" && item.id != wanted) { continue; }
		var instance = idOf(item);
		if (instance == 0UL) { continue; }
		return "{\"ok\":true,\"type\":\"" + item.id + "\",\"slot\":" + i + ",\"instanceId\":" + instance + "}";
	}

	return "{\"ok\":false,\"error\":\"not-found\",\"detail\":\"no body-slot item of type " + wanted + "\"}";
}))()
