// recipe: ui-sound-probe
// args: none
// returns: ok, played, detail
//
// Calls the game's own UI click entry (PlayerCamera.PlayUISound("click", 1f)),
// the call the overlay's report path makes; the sound recorder proves it ran.
((System.Func<string>)(() =>
{
	System.Type type = null;
	foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
	{
		type = assembly.GetType("PlayerCamera");
		if (type != null)
		{
			break;
		}
	}

	var method = type?.GetMethod("PlayUISound", new[] { typeof(string), typeof(float) });
	if (method == null)
	{
		return "{\"ok\":false,\"detail\":\"no-play-ui-sound\"}";
	}

	method.Invoke(null, new object[] { "click", 1f });
	return "{\"ok\":true,\"played\":\"click\"}";
}))()
