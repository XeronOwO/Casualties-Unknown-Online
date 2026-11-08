// recipe: native-heal
// args: none
// returns: ok, error
// serves: native-console-is-unpoliced
//
// Runs the game's own heal console command on this client by invoking the delegate the native console stores for it.
((System.Func<string>)(() => {
    var type = typeof(ConsoleScript);
    var commands = type.GetField("Commands", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).GetValue(null) as System.Collections.IList;
    if (commands == null) return "{\"ok\":false,\"error\":\"no command table\"}";
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
    foreach (var c in commands)
    {
        var nameField = c.GetType().GetField("name", flags);
        if (nameField == null) continue;
        if (!string.Equals(nameField.GetValue(c) as string, "heal", System.StringComparison.OrdinalIgnoreCase)) continue;
        foreach (var f in c.GetType().GetFields(flags))
        {
            var action = f.GetValue(c) as System.Delegate;
            if (action != null) { action.DynamicInvoke(new object[] { new string[0] }); return "{\"ok\":true,\"healed\":true}"; }
        }
        return "{\"ok\":false,\"error\":\"the heal command carries no delegate\"}";
    }
    return "{\"ok\":false,\"error\":\"no heal command\"}";
}))()
