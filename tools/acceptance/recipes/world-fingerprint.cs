// recipe: world-fingerprint
// args: none
// serves: world-determinism-world-fingerprint row 3, the world-determinism family
// returns: ok, world, error, detail
//
// Re-captures the product's own world fingerprint on demand. The runtime logs its
// [WorldFingerprint] line once per world entry (RunCoordinator, through WorldFingerprintLog),
// so a post-mutation pair cannot be taken by re-entering the world — this device calls the SAME
// diagnostic (WorldFingerprintLog.Log: FNV-1a over the live block table, eight chunks plus the
// total) through the plugin's own ILoggerFactory. Three clients whose re-captured lines agree
// chunk for chunk and in the total have identical block tables at the same moment.
// Read-only: nothing but the product's own log line is written.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"world\":false,\"error\":\"no-world\"}"; }
	const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
	System.Type loggerFactoryType = null;
	System.Type loggerType = null;
	System.Type logType = null;
	foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
	{
		if (loggerFactoryType == null) { loggerFactoryType = assembly.GetType("Microsoft.Extensions.Logging.ILoggerFactory", false); }
		if (loggerType == null) { loggerType = assembly.GetType("Microsoft.Extensions.Logging.ILogger", false); }
		if (logType == null) { logType = assembly.GetType("CasualtiesUnknownOnline.GameAdapter.Run.WorldFingerprintLog", false); }
	}
	if (loggerFactoryType == null || loggerType == null) { return "{\"ok\":false,\"world\":true,\"error\":\"no-logging\"}"; }
	var factory = services.GetService(loggerFactoryType);
	if (factory == null) { return "{\"ok\":false,\"world\":true,\"error\":\"no-logger-factory\"}"; }
	var create = loggerFactoryType.GetMethod("CreateLogger", new System.Type[] { typeof(string) });
	if (create == null) { return "{\"ok\":false,\"world\":true,\"error\":\"no-create-logger\"}"; }
	var logger = create.Invoke(factory, new object[] { "Acceptance.WorldFingerprint" });
	if (logger == null) { return "{\"ok\":false,\"world\":true,\"error\":\"no-logger\"}"; }
	if (logType == null) { return "{\"ok\":false,\"world\":true,\"error\":\"no-fingerprint-log\"}"; }
	System.Reflection.MethodInfo log = null;
	foreach (var candidate in logType.GetMethods(all))
	{
		var parameters = candidate.GetParameters();
		if (candidate.Name == "Log" && parameters.Length == 1 && parameters[0].ParameterType == loggerType) { log = candidate; break; }
	}
	if (log == null) { return "{\"ok\":false,\"world\":true,\"error\":\"no-log-method\"}"; }
	log.Invoke(null, new object[] { logger });
	return "{\"ok\":true,\"world\":true}";
}))()
