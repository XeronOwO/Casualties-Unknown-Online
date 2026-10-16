// recipe: recipe-unlock-forge
// args: index=n
// returns: ok, index, role, sessionActive, utc, error, detail
//
// Reports ONE blueprint unlock for the given recipe index through the product's own sender
// (ICraftControl.SendRecipeUnlock) — the same report path the game's own blueprint use drives, except that
// the index comes from the CALLER instead of from the table. The member side uses it to name an index no
// table has: the host must refuse it by name, apply nothing and relay nothing, while the same path with a
// real index is the control — and that CONTROL index must be one the host's own table holds (a real
// recipe index of that table): a second index no table has would be a second refusal reading, not a
// control. GUEST-ONLY: on a host that send arm applies locally and never reports, so this
// recipe refuses with not-guest instead of answering a false ok. One eval.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var index = (int)({{n:index}});
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
		return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"the unlock report path is the member's own; run this recipe on the guest\"}";
	}
	var craft = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.ICraftControl)) as CasualtiesUnknownOnline.Runtime.Session.Items.ICraftControl;
	if (craft == null) { return "{\"ok\":false,\"error\":\"no-craft-control\"}"; }
	var detail = "";
	try { craft.SendRecipeUnlock(index); }
	catch (System.Exception e) { detail = (e.GetType().Name + ": " + e.Message).Replace("\\", "/").Replace("\"", "'"); }
	if (detail.Length != 0) { return "{\"ok\":false,\"error\":\"send-failed\",\"detail\":\"" + detail + "\"}"; }
	return "{\"ok\":true"
		+ ",\"index\":" + index.ToString(inv)
		+ ",\"role\":\"Guest\""
		+ ",\"sessionActive\":" + (session.SessionActive ? "true" : "false")
		+ ",\"utc\":\"" + System.DateTime.UtcNow.ToString("o") + "\"}";
}))()
