// recipe: pointer-truth
// args: none
// returns: ok, mouseX, mouseY, worldX, worldY, remotes, error, detail
//
// The ground truth a drag-use hover needs: where the game's OWN `Input.mousePosition` reads (the virtual pointer is staged on it), the
// world point `Camera.main.ScreenToWorldPoint` makes of it, and every remote player's streamed position with its screen point. One read,
// so a batch can compare "the pointer's world point" against "the target's stream position" instead of guessing screen coordinates.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = UnityEngine.Camera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var mouse = UnityEngine.Input.mousePosition;
	var world = camera.ScreenToWorldPoint(mouse);
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var entitiesType = (System.Type)null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		if (entitiesType == null) { try { entitiesType = assemblies[i].GetType("CasualtiesUnknownOnline.Runtime.Session.EntitySync.IEntitySyncControl", false); } catch { entitiesType = null; } }
	}
	if (entitiesType == null) { return "{\"ok\":false,\"error\":\"no-entity-type\"}"; }
	var entities = services.GetService(entitiesType);
	if (entities == null) { return "{\"ok\":false,\"error\":\"no-entity-service\"}"; }
	var remotes = entitiesType.GetProperty("RemotePlayers").GetValue(entities, null) as System.Collections.IEnumerable;
	var parts = new System.Collections.Generic.List<string>();
	if (remotes != null) {
		foreach (var remote in remotes) {
			var id = remote.GetType().GetProperty("SteamId").GetValue(remote, null);
			var position = remote.GetType().GetProperty("Position").GetValue(remote, null);
			var px = System.Convert.ToSingle(position.GetType().GetField("X").GetValue(position));
			var py = System.Convert.ToSingle(position.GetType().GetField("Y").GetValue(position));
			var screen = camera.WorldToScreenPoint(new UnityEngine.Vector3(px, py, 0f));
			parts.Add("{\"steamId\":" + id + ",\"x\":" + px.ToString("0.##", inv) + ",\"y\":" + py.ToString("0.##", inv)
				+ ",\"screenX\":" + screen.x.ToString("0", inv) + ",\"screenY\":" + screen.y.ToString("0", inv) + "}");
		}
	}
	return "{\"ok\":true,\"mouseX\":" + mouse.x.ToString("0", inv) + ",\"mouseY\":" + mouse.y.ToString("0", inv)
		+ ",\"worldX\":" + ((double)world.x).ToString("0.##", inv) + ",\"worldY\":" + ((double)world.y).ToString("0.##", inv)
		+ ",\"remotes\":[" + string.Join(",", parts) + "]}";
}))()
