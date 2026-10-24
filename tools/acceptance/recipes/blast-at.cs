// recipe: blast-at
// args: x=n y=n
// serves: host-blast-rolls-a-second-drop-set (its count row needs a member-side blast that kills a shared building)
// returns: ok, x, y, error, detail
//
// Runs the GAME'S OWN explosion at a point on THIS client (`WorldGeneration.CreateExplosion`, the call a mine's own
// detonation and a turret's self-destruct end in). A member client that runs it beside a world-generation carrier destroys
// its own copy through the game's own damage path, which rolls that carrier's drops AND fires its carrier event — the
// member-side half of the row, without a gesture the driver cannot make.
//
// The params object is built statement by statement: an object initializer inside a recipe is one of the shapes this
// evaluator refuses as (1,1).
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = {{n:x}};
	var y = {{n:y}};
	var param = new ExplosionParams();
	param.position = new UnityEngine.Vector2(x, y);
	WorldGeneration.CreateExplosion(param);
	return "{\"ok\":true,\"x\":" + x.ToString("0.##", inv) + ",\"y\":" + y.ToString("0.##", inv) + "}";
}))()
