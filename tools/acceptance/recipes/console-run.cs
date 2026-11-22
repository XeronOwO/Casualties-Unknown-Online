// recipe: console-run
// args: line=s
// returns: ok, accepted, lines, last
//
// Submits one line through the console's own service — the same `ICommandControl.TryExecute` the overlay's Enter key calls —
// and answers whether it was accepted and what the console's own buffer says afterwards. The row about the console's
// completion needs this to prove two things without guessing: an unknown word is refused by the console rather than throwing,
// and a known command's answer lands in the very buffer the panel shows.
//
// Read-only in effect: it runs exactly what a player's Enter runs.
((System.Func<string>)(() => { var line = {{s:line}} ?? ""; var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services; if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; } var control = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Commands.ICommandControl)) as CasualtiesUnknownOnline.Runtime.Session.Commands.ICommandControl; if (control == null) { return "{\"ok\":false,\"error\":\"no-console-service\"}"; } var accepted = false; var error = ""; try { accepted = control.TryExecute(line); } catch (System.Exception thrown) { error = thrown.GetType().Name + ": " + thrown.Message; } var lines = control.Lines.Count; var last = ""; if (lines > 0) { last = control.Lines[lines - 1].Text ?? ""; } return "{\"ok\":true,\"accepted\":" + (accepted ? "true" : "false") + ",\"lines\":" + lines.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"last\":\"" + last.Replace("\\", "/").Replace("\"", "''").Replace("\n", " ").Replace("\r", " ") + "\",\"error\":\"" + error.Replace("\\", "/").Replace("\"", "''").Replace("\n", " ").Replace("\r", " ") + "\"}"; }))()
