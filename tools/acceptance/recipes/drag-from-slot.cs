// recipe: drag-from-slot
// args: slot=n
// returns: ok, slot, item, itemId, mouseX, mouseY, error, detail
//
// Hands the game's own drag bracket an item out of ONE body slot without a pixel: `PlayerCamera.dragItem` is the field
// `HandleReleaseDragging` reads, so setting it (and `clickPos`, which the drop branch compares against the pointer to tell a click
// from a drag) puts the release on exactly the code path a player's own drag ends on. The pickup half is deliberately skipped:
// the HUD cannot address a body slot whose widget carries another item (`MainMainHand` sits on it and answers empty), and the row
// this unblocks is the FULL-body feed, where the ring has nothing free to highlight at all (measured 2026-10-29, four presses).
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var wanted = {{n:slot}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
	var bodyField = typeof(PlayerCamera).GetField("body", flags);
	var body = bodyField == null ? null : bodyField.GetValue(camera);
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var getItem = body.GetType().GetMethod("GetItem");
	if (getItem == null) { return "{\"ok\":false,\"error\":\"no-GetItem\"}"; }
	var item = getItem.Invoke(body, new object[] { wanted });
	if (item == null) { return "{\"ok\":false,\"error\":\"empty-slot\",\"slot\":" + wanted.ToString(inv) + "}"; }
	var dragField = typeof(PlayerCamera).GetField("dragItem", flags);
	if (dragField == null) { return "{\"ok\":false,\"error\":\"no-drag-field\"}"; }
	var clickField = typeof(PlayerCamera).GetField("clickPos", flags);
	var mouse = UnityEngine.Input.mousePosition;
	if (clickField != null) { clickField.SetValue(camera, new UnityEngine.Vector2(mouse.x, mouse.y)); }
	dragField.SetValue(camera, item);
	var itemType = item.GetType();
	var idField = itemType.GetField("id");
	var itemId = idField == null ? "?" : (idField.GetValue(item) == null ? "?" : idField.GetValue(item).ToString());
	return "{\"ok\":true,\"slot\":" + wanted.ToString(inv) + ",\"item\":\"" + item.ToString().Replace("\"", "'")
		+ "\",\"itemId\":\"" + itemId + "\",\"mouseX\":" + ((int)mouse.x).ToString(inv) + ",\"mouseY\":" + ((int)mouse.y).ToString(inv) + "}";
}))()
