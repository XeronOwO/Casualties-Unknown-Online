// recipe: liquid-fill
// args: liquid=n
// serves: world-drink row 21 — standing in liquid, not calling the drink path from outside
// returns: ok, liquid, cellX, cellY, wasLiquid, error
//
// Puts the game's own liquid into the cell the local body stands in and does NOT drink: the body's own loop
// accumulates `liquidDrinkTime` and calls `FluidManager.DrinkLiquid` every two seconds
// (`Body.cs:3433-3437`), which is the path a player standing in water takes. `liquid` is the game's own type:
// 5 water, 6 refuse (the refusal branch). The row fills, waits, and reads the body's water afterwards.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var liquid = {{n:liquid}};
	var world = WorldGeneration.world;
	var fluids = FluidManager.main;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (world == null || fluids == null || body == null) { return "{\"ok\":false,\"error\":\"no-world-or-body\"}"; }
	var cell = world.WorldToBlockPos(body.transform.position);
	var wasLiquid = fluids.GetLiquid(cell.x, cell.y);
	fluids.SetLiquid(cell.x, cell.y, (byte)liquid);
	return "{\"ok\":true,\"liquid\":" + ((int)liquid).ToString(inv)
		+ ",\"cellX\":" + cell.x.ToString(inv)
		+ ",\"cellY\":" + cell.y.ToString(inv)
		+ ",\"wasLiquid\":" + ((int)wasLiquid).ToString(inv) + "}";
}))()
