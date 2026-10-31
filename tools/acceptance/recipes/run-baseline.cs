// recipe: run-baseline
// args: none
// returns: ok, hasCommittedRunBaseline, worldParams, epoch, queryRun, error, detail
//
// Reads the three facts the re-entry policy decides on, on THIS client: whether the world domain believes a run baseline is
// committed, whether it holds world params, and — through any service that exposes them — the kernel's run epoch and its run.
// The point is to separate "the world is up" from "the kernel has a run", which are different facts and were seen disagreeing
// (a host in a world whose re-entry arm refused with "no committed run baseline").
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	var worldType = (System.Type)null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (worldType == null) { try { worldType = assemblies[i].GetType("CasualtiesUnknownOnline.Runtime.Session.World.IWorldControl", false); } catch { worldType = null; } }
	}
	if (worldType == null) { return "{\"ok\":false,\"error\":\"no-world-type\"}"; }
	var world = services.GetService(worldType);
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world-service\"}"; }
	var baseline = worldType.GetProperty("HasCommittedRunBaseline").GetValue(world, null).ToString().ToLowerInvariant();
	var paramsProperty = worldType.GetProperty("WorldParams");
	var worldParams = paramsProperty == null ? "missing" : (paramsProperty.GetValue(world, null) == null ? "null" : "present");

	var epoch = "none";
	var queryRun = "none";
	for (var s = 0; s < assemblies.Length && queryRun == "none"; s++) {
		System.Type[] exported;
		try { exported = assemblies[s].GetExportedTypes(); } catch { continue; }
		for (var t = 0; t < exported.Length; t++) {
			var type = exported[t];
			var query = type.GetMethod("QueryRun", flags, null, System.Type.EmptyTypes, null);
			var epochProperty = type.GetProperty("CurrentRunEpoch", flags);
			if (query == null && epochProperty == null) { continue; }
			var service = services.GetService(type);
			if (service == null) { continue; }
			if (epochProperty != null) {
				var value = epochProperty.GetValue(service, null);
				var inner = value == null ? null : value.GetType().GetProperty("Value", flags);
				epoch = inner != null ? inner.GetValue(value, null).ToString() : value == null ? "null" : value.ToString();
			}
			if (query != null) {
				queryRun = query.Invoke(service, null) == null ? "null" : "present";
			}
		}
	}

	return "{\"ok\":true,\"hasCommittedRunBaseline\":" + baseline + ",\"worldParams\":\"" + worldParams + "\",\"epoch\":\"" + epoch + "\",\"queryRun\":\"" + queryRun + "\"}";
}))()
