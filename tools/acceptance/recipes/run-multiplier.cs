// recipe: run-multiplier
// args: mode=s loot=n trap=n
// returns: ok, mode, loot, trap, error, detail
//
// Reads or sets the live world's two run-rarity multipliers (WorldGeneration.lootRarityMultiplier /
// .trapRarityMultiplier, the accumulated values the game's own generator reads through LootDensity/TrapDensity).
// mode=read returns them; mode=set writes the given values and returns them back (the read mode still needs the two
// numeric args because the driver requires every declared arg). Reflection-only: the game's types are not nameable
// from the eval scope.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var lootValue = {{n:loot}};
	var trapValue = {{n:trap}};
	System.Type genType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length && genType == null; i++) {
		try { genType = assemblies[i].GetType("WorldGeneration", false); } catch { genType = null; }
	}
	if (genType == null) { return "{\"ok\":false,\"error\":\"no-world-generation-type\"}"; }
	var worldField = genType.GetField("world", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
	if (worldField == null) { return "{\"ok\":false,\"error\":\"no-world-field\"}"; }
	var world = worldField.GetValue(null);
	if (world == null) { return "{\"ok\":false,\"error\":\"no-live-world\"}"; }
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var lootField = genType.GetField("lootRarityMultiplier", flags);
	var trapField = genType.GetField("trapRarityMultiplier", flags);
	if (lootField == null || trapField == null) { return "{\"ok\":false,\"error\":\"no-multiplier-fields\"}"; }
	if (mode == "set") {
		lootField.SetValue(world, System.Convert.ToSingle(lootValue));
		trapField.SetValue(world, System.Convert.ToSingle(trapValue));
	}
	var loot = ((System.Single)lootField.GetValue(world)).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
	var trap = ((System.Single)trapField.GetValue(world)).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"loot\":\"" + loot + "\",\"trap\":\"" + trap + "\"}";
}))()
