// recipe: console-registry
// args: none
// returns: ok, commands, lines, names, error, detail
//
// Reads the SAME service twice: the filtered command projection the console UI and the completion both use
// (`ICommandControl.Commands`), and its own line buffer (`Lines`). `commands=0` with `lines>0` proves the projection's authority
// filter rejects everything on a console that is demonstrably alive (it printed its own /help output into that buffer), which is a
// different finding from "this container holds another instance".
((System.Func<string>)(() => { var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services; if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; } var control = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Commands.ICommandControl)) as CasualtiesUnknownOnline.Runtime.Session.Commands.ICommandControl; if (control == null) { return "{\"ok\":false,\"error\":\"no-command-control\"}"; } var all = control.Commands; var names = new System.Text.StringBuilder(); for (var i = 0; i < all.Count && i < 8; i++) { if (i > 0) { names.Append(','); } names.Append(all[i].Name); } var lines = 0; var linesProperty = control.GetType().GetProperty("Lines"); if (linesProperty != null) { var buffer = linesProperty.GetValue(control) as System.Collections.ICollection; if (buffer != null) { lines = buffer.Count; } } return "{\"ok\":true,\"commands\":" + all.Count + ",\"lines\":" + lines + ",\"names\":\"" + names + "\"}"; }))()