// recipe: console-registry
// args: none
// returns: ok, commands, lines, names, error, detail
//
// Reads the SAME service twice: the filtered command projection the console UI and the completion both use
// (`Commands`), and its own line buffer (`Lines`). `commands=0` with `lines>0` proves the projection's authority
// filter rejects everything on a console that is demonstrably alive (it printed its own /help output into that buffer), which is a
// different finding from "this container holds another instance".
((System.Func<string>)(() => { var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services; if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; } var control = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Commands.ICommandControl)); if (control == null) { return "{\"ok\":false,\"error\":\"no-console-service\"}"; } var commandsProperty = control.GetType().GetProperty("Commands"); if (commandsProperty == null) { return "{\"ok\":false,\"error\":\"no-commands-projection\"}"; } var all = commandsProperty.GetValue(control) as System.Collections.IEnumerable; if (all == null) { return "{\"ok\":false,\"error\":\"no-commands\"}"; } var names = new System.Text.StringBuilder(); var count = 0; foreach (var command in all) { if (count < 8) { if (count > 0) { names.Append(','); } var name = command.GetType().GetProperty("Name"); names.Append(name == null ? "?" : name.GetValue(command)); } count++; } var lines = ((CasualtiesUnknownOnline.Runtime.Session.Commands.ICommandControl)control).Lines.Count; return "{\"ok\":true,\"commands\":" + count + ",\"lines\":" + lines + ",\"names\":\"" + names + "\"}"; }))()
