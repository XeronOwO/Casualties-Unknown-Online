// recipe: cut-request
// args: none
// serves: save-mid-run-consistent-cut row 4 (an in-flight state must resolve before the cut)
// returns: ok, asked, refusal, error, detail
//
// Asks the product's own save control for a Command cut (`IWorldSaveControl.TryRequestCut`, the call /save makes) and
// reports the request's answer. Used against an in-flight transient (a trap's drop hold) so the deferral is read from the
// host log rather than a one-second console round trip.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var asms = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type controlType = null;
	System.Type reasonType = null;
	for (var i = 0; i < asms.Length; i++) {
		if (controlType == null) { try { controlType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Session.Persistence.IWorldSaveControl", false); } catch { controlType = null; } }
		if (reasonType == null) { try { reasonType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Persistence.WorldCutReason", false); } catch { reasonType = null; } }
		if (controlType != null && reasonType != null) { break; }
	}
	if (controlType == null) { return "{\"ok\":false,\"error\":\"no-control-type\"}"; }
	if (reasonType == null) { return "{\"ok\":false,\"error\":\"no-reason-type\"}"; }
	var control = services.GetService(controlType);
	if (control == null) { return "{\"ok\":false,\"error\":\"no-save-control\"}"; }
	var reason = System.Enum.Parse(reasonType, "Command");
	var args = new object[] { reason, null };
	var asked = (bool)controlType.GetMethod("TryRequestCut").Invoke(control, args);
	return "{\"ok\":true,\"asked\":" + (asked ? "true" : "false")
		+ ",\"refusal\":" + (args[1] == null ? "null" : "\"" + args[1].ToString().Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"") + "}";
}))()
