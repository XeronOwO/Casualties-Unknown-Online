// recipe: radline-timer-read
// args: none
// serves: radiation-line-descent-rate-per-side
// returns: ok, result, error, detail
//
// Reads `AcceptanceRadlineTimer.Read()` from the client's own live world: the layer clock, the layer limit,
// the biome override and the line's active flag — the four facts the vanilla activation is decided from.
((System.Func<string>)(() => {
	System.Type type = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++)
	{
		type = assemblies[i].GetType("AcceptanceRadlineTimer", false);
		if (type != null) { break; }
	}
	if (type == null) { return "{\"ok\":false,\"error\":\"no-declaration\"}"; }
	var read = type.GetMethod("Read", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
	if (read == null) { return "{\"ok\":false,\"error\":\"no-method\"}"; }
	var value = (string)read.Invoke(null, null);
	return "{\"ok\":true,\"result\":\"" + value + "\"}";
}))()
