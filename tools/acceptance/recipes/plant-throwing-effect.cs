// recipe: plant-throwing-effect
// args: none
// serves: mod-authored-effects, mod-item-authored-actions
// returns: ok, mod, itemRegistered, liquidRegistered, error
//
// Plants the one fixture the tree does not ship: a mod function that THROWS. Both registration faces
// guard their invocation (and the timed body guards every tick), and their rows say so in prose —
// "source-verified, rides the acceptance row" — because a throw cannot be produced by the example mod's
// own registered functions. This recipe supplies it through the PRODUCT's own registration surface: it
// resolves the example mod's own `IModContext` out of the running client and registers
//   * a use action for `example.placebo` (which declares `Usable` and registers nothing, so the
//     reachability rail accepts it), and
//   * a limb-use effect for `example.tonic` (whose limb half the fixture leaves unregistered on purpose),
// each of which writes something the run can read, starts a mod timed effect whose tick throws once its
// remaining duration is under half, and then throws itself. Driving the gesture afterwards therefore
// exercises the call guard and, one game second later, the TICK guard — several times over, which is
// what tells "the effect keeps its schedule" apart from "it threw once and stopped".
//
// The registered delegates contain LITERALS only: the evaluator's Mono REPL refuses a lambda that
// captures a local, so a parameterized version of this recipe cannot be written with a substitution inside
// a lambda body (and a recipe's declared argument may appear exactly once anyway). A registration the
// rails refuse answers false and the run reads that, rather than assuming the fixture landed.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type serviceType = null;
	for (var i = 0; i < assemblies.Length; i++) {
		System.Type candidate = null;
		try { candidate = assemblies[i].GetType("CasualtiesUnknownOnline.Runtime.Session.Mods.ModService", false); } catch { candidate = null; }
		if (candidate != null) { serviceType = candidate; break; }
	}
	if (serviceType == null) { return "{\"ok\":false,\"error\":\"no-modservice-type\"}"; }
	var getService = services.GetType().GetMethod("GetService", new System.Type[] { typeof(System.Type) });
	var modService = getService != null ? getService.Invoke(services, new object[] { serviceType }) : null;
	if (modService == null) { return "{\"ok\":false,\"error\":\"no-modservice\"}"; }
	var loadedProperty = serviceType.GetProperty("LoadedMods", flags);
	var loaded = loadedProperty != null ? loadedProperty.GetValue(modService, null) as System.Collections.IEnumerable : null;
	if (loaded == null) { return "{\"ok\":false,\"error\":\"no-loaded-mods\"}"; }
	// The example mod's own context: the loaded mod instances are the only public way in, and each one
	// holds the context the framework handed it at Bind.
	CasualtiesUnknownOnline.Abstractions.IModContext context = null;
	var modName = "";
	foreach (var mod in loaded) {
		var type = mod.GetType();
		if (type.Name != "ExampleMod") { continue; }
		var field = type.GetField("_context", flags);
		var value = field != null ? field.GetValue(mod) as CasualtiesUnknownOnline.Abstractions.IModContext : null;
		if (value == null) { continue; }
		context = value;
		modName = type.FullName;
		break;
	}
	if (context == null) { return "{\"ok\":false,\"error\":\"no-example-mod-context\"}"; }
	var itemRegistered = context.ItemActions.TryRegisterUse("example.placebo", action => {
		action.Item.AddCondition(-0.05f);
		action.Body.StartTimedEffect("acceptance-guard", 5f, timed => {
			if (timed.TimedEffectRemaining("acceptance-guard") < 2.5f) { throw new System.Exception("acceptance tick guard probe"); }
		});
		throw new System.Exception("acceptance call guard probe");
	});
	var liquidRegistered = context.LiquidEffects.TryRegisterLimbUse("example.tonic", effect => {
		effect.Body.StartTimedEffect("acceptance-guard-liquid", 5f, timed => {
			if (timed.TimedEffectRemaining("acceptance-guard-liquid") < 2.5f) { throw new System.Exception("acceptance tick guard probe"); }
		});
		throw new System.Exception("acceptance call guard probe");
	});
	return "{\"ok\":true,\"mod\":\"" + modName + "\""
		+ ",\"itemRegistered\":" + (itemRegistered ? "true" : "false")
		+ ",\"liquidRegistered\":" + (liquidRegistered ? "true" : "false") + "}";
}))()
