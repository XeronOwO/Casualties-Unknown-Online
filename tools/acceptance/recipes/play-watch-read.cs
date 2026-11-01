// recipe: play-watch-read
// args: none
// returns: ok, count, calls, error, detail
//
// Reads what `play-watch` recorded: how many `Sound.Play(AudioClip, …)` calls this client has made since the prefix was installed and the clip name of each. A
// clip the receive handler was told to replay but that never appears here is the proof that the replay stopped before the engine call, which is the last gap
// on the lockpick-pain ticket.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type watch = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { watch = assemblies[i].GetType("AcceptancePlayWatch", false); } catch { watch = null; }
		if (watch != null) { break; }
	}
	if (watch == null) { return "{\"ok\":false,\"error\":\"not-declared\"}"; }
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var countField = watch.GetField("Count", flags);
	var callsField = watch.GetField("Calls", flags);
	var count = countField == null ? -1 : System.Convert.ToInt32(countField.GetValue(null));
	var calls = callsField == null ? "" : (string)callsField.GetValue(null);
	return "{\"ok\":true,\"count\":" + count.ToString(inv) + ",\"calls\":\"" + calls + "\"}";
}))()
