// recipe: item-wear
// args: slot=n
// serves: container-move-snapshot-only-sync row A1h (the remote-driven DropWearable needs an OWNER that
//         wears a wearable; the game's only wear gesture is the radial centre -- see
//         docs/acceptance/lessons.md)
// returns: ok, type, worn, error
//
// Dresses the local body through the game's own Body.WearWearable: the OWNER-side state the DropWearable
// row needs, not the gesture that produces the intent. The intent comes from the operator's own
// remote-gesture release of the worn proxy, so the owner's wardrobe is scene setup -- the same class as
// item-provide's Utils.Create + Body.PickUpItem. Body.GetWearable(type) is the same native query
// RemoteIntentApplier.ApplyDropWearable reads, and it is read back here, so the answer says whether the
// fixture actually landed.
((System.Func<string>)(() => {
	var slotArgument = {{n:slot}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var item = body.GetItem(slotArgument);
	if (item == null) { return "{\"ok\":false,\"error\":\"no-item\"}"; }
	var type = item.id;
	body.WearWearable(item);
	return "{\"ok\":true,\"type\":\"" + type + "\",\"worn\":" + (body.GetWearable(type) != null ? "true" : "false") + "}";
}))()
