// recipe: drag-start
// args: type=s
// returns: ok, id, name, count, ids, error, detail
//
// Arms the game's drag gesture: `PlayerCamera.main.dragItem` is the field the mouse-down path fills (`TryPickupFromUI`), and the
// release path (`HandleReleaseDragging` -> `TryPerformUIActions`) turns a drag onto the wound view's limb image into a treatment.
// The item comes from `Body.GetAllItemsThorough()` (Body.cs:532), which walks the body's slots and wearables - the same set the
// game's own pickup can reach. The caller stages the pointer and the iteminteract press/release, so the decision stays the game's.
// The wanted-id test: an empty request takes the first item the body carries.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var wanted = {{s:type}};

	var items = camera.body.GetAllItemsThorough();
	if (items == null) { return "{\"ok\":false,\"error\":\"no-items\"}"; }
	var ids = "";
	var hit = "";
	for (var i = 0; i < items.Count; i++)
	{
		var candidate = items[i];
		if (candidate == null) { continue; }
		var itemId = candidate.id;
		ids = ids + itemId + "|";
		if ((wanted.Length == 0 || wanted == itemId) && hit.Length == 0)
		{
			camera.dragItem = candidate;
			hit = itemId;
		}
	}

	if (hit.Length == 0) { return "{\"ok\":false,\"error\":\"not-carried\",\"count\":" + items.Count + ",\"ids\":\"" + ids + "\"}"; }
	return "{\"ok\":true,\"id\":\"" + hit + "\",\"count\":" + items.Count + ",\"ids\":\"" + ids + "\"}";
}))()
