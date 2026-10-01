// recipe: console-read
// args: count=n
// serves: save-restore-account-surface, save-new-player-starting-supplies,
//         save-interval-autosave-and-backup-recovery, save-layer-end-save-and-restore
// returns: ok, count, lines, error, detail
//
// Reads the CUO command console's own history (CommandConsoleService.Lines), so a record can cite
// exactly what the player could read: the newest `count` lines, oldest first.
((System.Func<string>)(() => {
	var count = {{n:count}};
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	var console = services == null ? null : services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Commands.CommandConsoleService));
	if (console == null) { return "{\"ok\":false,\"error\":\"no-console-service\"}"; }
	var lines = ((CasualtiesUnknownOnline.Runtime.Session.Commands.CommandConsoleService)console).Lines;
	var start = lines.Count - count;
	if (start < 0) { start = 0; }
	var builder = new System.Text.StringBuilder();
	builder.Append("{\"ok\":true,\"count\":").Append(lines.Count).Append(",\"lines\":[");
	for (var i = start; i < lines.Count; i++) {
		if (i > start) { builder.Append(','); }
		var text = lines[i].Text == null ? "" : lines[i].Text;
		builder.Append('"').Append(text.Replace('"', '\'').Replace('\\', '/')).Append('"');
	}
	builder.Append("]}");
	return builder.ToString();
}))()
