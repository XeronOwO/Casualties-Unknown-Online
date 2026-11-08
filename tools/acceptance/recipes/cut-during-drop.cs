// recipe: cut-during-drop
// args: type=s
// serves: save-mid-run-consistent-cut row 4 (an in-flight state must resolve before the cut)
// returns: ok, item, dropped, thrown, asked, refusal, error, detail
//
// Drops a carried item through the game's own release path and asks for a Command cut IN THE SAME FRAME: the drop's
// flush is a ResolveBeforeSave transient, so the cut must wait for it and log "the Command cut is waiting for in-flight
// state to resolve". The item is found by prefab id (empty takes the first carried item).
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var wanted = {{s:type}};
	var items = camera.body.GetAllItemsThorough();
	if (items == null) { return "{\"ok\":false,\"error\":\"no-items\"}"; }
	var hit = "";
	for (var i = 0; i < items.Count; i++) {
		var candidate = items[i];
		if (candidate == null) { continue; }
		if ((wanted.Length == 0 || wanted == candidate.id) && hit.Length == 0) { camera.dragItem = candidate; hit = candidate.id; }
	}
	if (hit.Length == 0) { return "{\"ok\":false,\"error\":\"no-item\"}"; }
	var release = typeof(PlayerCamera).GetMethod("HandleReleaseDragging", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
	if (release == null) { return "{\"ok\":false,\"error\":\"no-handle-release\"}"; }
	var casts = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
	var thrown = "";
	try { release.Invoke(camera, new object[] { casts }); } catch (System.Exception ex) { thrown = ex.GetType().Name + ":" + (ex.InnerException != null ? ex.InnerException.Message : ex.Message); }
	var dropped = camera.dragItem == null;

	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\",\"item\":\"" + hit + "\",\"dropped\":" + (dropped ? "true" : "false") + "}"; }
	var asms = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type controlType = null;
	System.Type reasonType = null;
	for (var i = 0; i < asms.Length; i++) {
		if (controlType == null) { try { controlType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Session.Persistence.IWorldSaveControl", false); } catch { controlType = null; } }
		if (reasonType == null) { try { reasonType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Persistence.WorldCutReason", false); } catch { reasonType = null; } }
		if (controlType != null && reasonType != null) { break; }
	}
	if (controlType == null) { return "{\"ok\":false,\"error\":\"no-control-type\",\"item\":\"" + hit + "\",\"dropped\":" + (dropped ? "true" : "false") + "}"; }
	if (reasonType == null) { return "{\"ok\":false,\"error\":\"no-reason-type\",\"item\":\"" + hit + "\",\"dropped\":" + (dropped ? "true" : "false") + "}"; }
	var control = services.GetService(controlType);
	if (control == null) { return "{\"ok\":false,\"error\":\"no-save-control\",\"item\":\"" + hit + "\",\"dropped\":" + (dropped ? "true" : "false") + "}"; }
	var reason = System.Enum.Parse(reasonType, "Command");
	var args = new object[] { reason, null };
	var asked = (bool)controlType.GetMethod("TryRequestCut").Invoke(control, args);
	return "{\"ok\":true,\"item\":\"" + hit + "\",\"dropped\":" + (dropped ? "true" : "false")
		+ ",\"thrown\":\"" + thrown.Replace("\"", "'") + "\""
		+ ",\"asked\":" + (asked ? "true" : "false")
		+ ",\"refusal\":" + (args[1] == null ? "null" : "\"" + args[1].ToString().Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"") + "}";
}))()
