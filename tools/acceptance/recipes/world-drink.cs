// recipe: world-drink
// args: liquid=n
// serves: unhooked-item-and-body-sound-families
// returns: ok, liquid, cellX, cellY, error, detail
//
// Puts a liquid into the cell the local body stands in and drinks it through the game's own
// FluidManager.DrinkLiquid, the scope the world-drink family reports from. `liquid` is the game's own
// type: 1 groundwater, 2 lumalgae, 3 oil, 4 sap, 5 water (its own "drink" call), 6 refuse (the
// refusal branch). One call per invocation; the run repeats it per branch.
((System.Func<string>)(() => {
	var liquid = {{n:liquid}};
	var world = WorldGeneration.world;
	var fluids = FluidManager.main;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (world == null || fluids == null || body == null) { return "{\"ok\":false,\"error\":\"no-world-or-body\"}"; }
	var cell = world.WorldToBlockPos(body.transform.position);
	fluids.SetLiquid(cell.x, cell.y, (byte)liquid);
	fluids.DrinkLiquid(cell, body);
	return "{\"ok\":true,\"liquid\":" + ((int)liquid).ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"cellX\":" + cell.x.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"cellY\":" + cell.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
