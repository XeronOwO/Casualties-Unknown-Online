// recipe: drag-start
// args: type=s
// returns: ok, id, name, count, error, detail
//
// Arms the game's drag gesture: `PlayerCamera.main.dragItem` is the field the mouse-down path fills (`TryPickupFromUI`), and the
// release path (`HandleReleaseDragging` -> `TryPerformUIActions`) turns a drag onto the wound view's limb image into a treatment.
// The item comes from `Body.GetAllItemsThorough()` (Body.cs:532), which walks hand slots, inventory and wearables - the same set the
// game's own pickup can reach. The caller stages the pointer and the Mouse0 release.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var wanted = {{s:type}};
	var items = camera.body.GetAllItemsThorough();
	if (items == null) { return "{\"ok\":false,\"error\":\"no-items\"}"; }
	var count = items.Count;
	Item hit = null;
	for (var i = 0; i < items.Count; i++)
	{
		var candidate = items[i];
		if (candidate == null) { continue; }
		count++;
		var itemId = candidate.id;
		ids = ids + itemId + "|";
		if (wanted.Length > 0 && itemId != wanted) { continue; }
		hit = candidate;
		break;
	}

	if (hit == null) { return "{\"ok\":false,\"error\":\"not-carried\",\"count\":" + count + ",\"ids\":\"" + ids + "\"}"; }
	camera.dragItem = hit;
	return "{\"ok\":true,\"id\":\"" + hit.id + "\",\"name\":\"" + hit.gameObject.name + "\",\"count\":" + count + "}";
}))()