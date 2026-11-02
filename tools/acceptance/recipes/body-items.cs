// recipe: body-items
// args: none
// returns: ok, body, slots, hand, ids, thorough, wearables, error, detail
//
// Lists what the LOCAL body actually holds, from the two walks the game itself uses: `Body.slots` through `GetItem(i)` (what
// `GetAllItemsThorough` starts from, Body.cs:532-570) and `GetAllWearables()`. It exists because `drag-start` answered an empty
// item list on a body the console had just filled, and the walk needs to be read one layer lower.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var body = camera.body;
	var slots = body.slots == null ? -1 : body.slots.Length;
	var ids = "";
	var filled = 0;
	if (body.slots != null)
	{
		for (var i = 0; i < body.slots.Length; i++)
		{
			var item = body.GetItem(i);
			if (item == null) { continue; }
			filled++;
			if (i < 24) { ids = ids + i + ":" + item.id + "|"; }
		}
	}

	var thorough = body.GetAllItemsThorough();
	var wearables = body.GetAllWearables();
	return "{\"ok\":true,\"body\":\"" + body.gameObject.name + "\",\"slots\":" + slots + ",\"filled\":" + filled
		+ ",\"hand\":" + body.handSlot
		+ ",\"thorough\":" + (thorough == null ? -1 : thorough.Count)
		+ ",\"wearables\":" + (wearables == null ? -1 : wearables.Count)
		+ ",\"ids\":\"" + ids + "\"}";
}))()