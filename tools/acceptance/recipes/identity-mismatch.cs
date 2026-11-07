// recipe: identity-mismatch
// args: dimension=s value=s
// returns: ok, result, error
//
// Asks the already-declared `identity-mismatch` class to send ONE guest→host handshake whose named identity dimension differs,
// over the live session, then reports what the send primitive answered. The host's refusal log line and the guest's rendered
// refusal line are the readings this recipe exists for.
((System.Func<string>)(() => {
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type declaration = null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (declaration == null) { try { declaration = assemblies[i].GetType("AcceptanceIdentityMismatch", false); } catch { declaration = null; } }
	}
	if (declaration == null) { return "{\"ok\":false,\"error\":\"not-declared\"}"; }
	var method = declaration.GetMethod("Send", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
	if (method == null) { return "{\"ok\":false,\"error\":\"no-send\"}"; }
	var sent = (string)method.Invoke(null, new object[] { {{s:dimension}}, {{s:value}} });
	return "{\"ok\":true,\"result\":\"" + (sent == null ? "" : sent.Replace("\\", "/").Replace("\"", "'")) + "\"}";
}))()
