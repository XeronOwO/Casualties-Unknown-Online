// recipe: report-watch-read
// args: none
// returns: ok, count, calls, error, detail
//
// Reads what `report-watch` recorded: how many character-sound reports this client made since the prefix was installed, and the arguments of each (kind,
// clip, position, volume, followOwner, twoDimensional, recoil). A count of zero across an event the client audibly played is the proof that the capture
// never reached the report, which is the question the lockpick-pain ticket was left with after every other candidate had been ruled out by reading.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type watch = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { watch = assemblies[i].GetType("AcceptanceReportWatch", false); } catch { watch = null; }
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
