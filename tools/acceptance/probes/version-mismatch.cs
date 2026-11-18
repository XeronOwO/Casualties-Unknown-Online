// recipe: version-mismatch
// args: protocol=n
// returns: ok, result, error
//
// Asks the already-declared `version-mismatch` class to send ONE guest→host handshake carrying the given protocol number
// over the live session, then reports what the send primitive answered. The host's own log line names the peer's number and
// its own (HandshakeHandler), which is the reading this recipe exists for.
((System.Func<string>)(() => {
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type declaration = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (declaration == null) { try { declaration = assemblies[i].GetType("AcceptanceVersionMismatch", false); } catch { declaration = null; } }
	}
	if (declaration == null) { return "{\"ok\":false,\"error\":\"not-declared\"}"; }
	var method = declaration.GetMethod("Send", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
	if (method == null) { return "{\"ok\":false,\"error\":\"no-send\"}"; }
	var sent = (string)method.Invoke(null, new object[] { {{n:protocol}} });
	return "{\"ok\":true,\"result\":\"" + (sent == null ? "" : sent.Replace("\\", "/").Replace("\"", "'")) + "\"}";
}))()
