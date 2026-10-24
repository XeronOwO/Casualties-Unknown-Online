// recipe: blast-at
// args: x=n y=n
// serves: host-blast-rolls-a-second-drop-set (its count row needs a member-side blast that kills a shared building)
// returns: ok, x, y, found, error, detail
//
// Runs the GAME'S OWN explosion at a point on THIS client: `WorldGeneration.CreateExplosion` is the call a mine's own
// detonation and a turret's self-destruct end in, so a member client that runs it beside a world-generation carrier
// destroys its own copy through the game's damage path — which rolls that carrier's drops AND fires its carrier event.
//
// Reached by REFLECTION on purpose: naming `ExplosionParams` in a recipe is one of the shapes this evaluator refuses as
// (1,1), while reflection over the same method compiles (the pattern runtime-entity-tables already uses).
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = {{n:x}};
	var y = {{n:y}};
	var method = typeof(WorldGeneration).GetMethod("CreateExplosion", flags);
	if (method == null) { return "{\"ok\":false,\"error\":\"no-explosion-entry\"}"; }
	var parameter = method.GetParameters()[0];
	var param = System.Activator.CreateInstance(parameter.ParameterType);
	var positionField = parameter.ParameterType.GetField("position", flags);
	if (positionField == null) { return "{\"ok\":false,\"error\":\"no-position-field\"}"; }
	positionField.SetValue(param, new UnityEngine.Vector2(x, y));
	method.Invoke(null, new object[] { param });
	return "{\"ok\":true,\"found\":true,\"x\":" + x.ToString("0.##", inv) + ",\"y\":" + y.ToString("0.##", inv) + "}";
}))()
