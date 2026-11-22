// recipe: swing-once
// args: none
// returns: ok, before, after, called, error
//
// Calls the game's own swing entry once and reports whether the swing counter saw it. It answers two questions at once:
// whether the counter's patch is live on this build, and whether an empty hand really ends in `DoUnarmedAttack`
// (`Body.cs:2718-2732`: `UseItemInHand` falls through to it when the hand holds nothing).
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var counter = new System.Func<long>(() => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var a = 0; a < assemblies.Length; a++)
		{
			System.Type[] types;
			try { types = assemblies[a].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++)
			{
				if (types[t].Name != "VirtualInput") { continue; }
				var property = types[t].GetProperty("Swings", flags);
				if (property != null) { return System.Convert.ToInt64(property.GetValue(null, null), inv); }
			}
		}
		return -1L;
	});
	var before = counter();
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"before\":" + before.ToString(inv) + ",\"error\":\"no-body\"}"; }
	var error = "";
	var called = false;
	try
	{
		camera.body.UseItemInHand();
		called = true;
	}
	catch (System.Exception exception) { error = exception.GetType().Name + ": " + exception.Message; }
	var after = counter();
	return "{\"ok\":true,\"before\":" + before.ToString(inv) + ",\"after\":" + after.ToString(inv) + ",\"called\":" + (called ? "true" : "false") + ",\"error\":\"" + error.Replace("\\", "/").Replace("\"", "'") + "\"}";
}))()
