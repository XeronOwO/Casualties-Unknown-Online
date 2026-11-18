// recipe: item-forge
// args: mode=s kind=s id=s
// serves: item-creation-registration-first rows 2, 5 and 6 (the later-operation drive on a refused or never-judged id)
// returns: ok, mode, kind, id, utc, error, detail
//
// Sends ONE item operation for the given instance id through the product's own sender
// (IItemControl.SendItemPickedUp / SendItemDestroyed) — the same report path a native pickup or a
// native destroy drives, with the same creation-before-operation gate in front of it. The run uses
// it to put an operation on the wire for an id the host refused (row 2/5: a break that lost
// first-writer-wins recorded a tombstone) or never judged (row 6): the host must answer with the
// tombstone's precise reason or the protocol-violation refusal — never hold, never materialize.
// GUEST-ONLY: the send branches inside the product are guest-only, so on a host this recipe sends
// nothing and refuses with not-guest instead of answering a false ok. mode=op with
// kind=pickup|destroy. One eval.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var kind = {{s:kind}};
	var rawId = {{s:id}};
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var items = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.IItemControl)) as CasualtiesUnknownOnline.Runtime.Session.Items.IItemControl;
	if (items == null) { return "{\"ok\":false,\"error\":\"no-item-control\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
		return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"the operation report path is guest-only; run this recipe on the member, not the host\"}";
	}
	var id = 0UL;
	if (!ulong.TryParse(rawId, System.Globalization.NumberStyles.None, inv, out id) || id == 0UL) {
		return "{\"ok\":false,\"error\":\"bad-id\",\"detail\":\"id must be a non-zero decimal item instance id\"}";
	}
	if (mode != "op") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be op\"}"; }
	if (kind != "pickup" && kind != "destroy") { return "{\"ok\":false,\"error\":\"bad-kind\",\"detail\":\"kind must be pickup or destroy\"}"; }
	var detail = "";
	try {
		if (kind == "pickup") { items.SendItemPickedUp(id, null); }
		else { items.SendItemDestroyed(id); }
	}
	catch (System.Exception e) { detail = (e.GetType().Name + ": " + e.Message).Replace("\\", "/").Replace("\"", "'"); }
	if (detail.Length != 0) { return "{\"ok\":false,\"error\":\"send-failed\",\"detail\":\"" + detail + "\"}"; }
	return "{\"ok\":true,\"mode\":\"op\",\"kind\":\"" + kind + "\",\"id\":\"" + id.ToString(inv)
		+ "\",\"utc\":\"" + System.DateTime.UtcNow.ToString("o") + "\"}";
}))()
