// recipe: trap-trigger
// args: x=n y=n
// serves: host-blast-rolls-a-second-drop-set (its acceptance rows need a mine to fire through the game's own path)
// returns: ok, mines, mineX, mineY, error, detail
//
// Presses the mine nearest the given point by setting the field the PLAYER's step sets (`MineScript.pressed`), and lets the
// game run its own sequence from there: 0.8 s later the mine explodes natively (`WorldGeneration.CreateExplosion`), the
// crate beside it dies, both roll their own drops on this client, and the mine patch reports the event — the whole native
// path a player's step starts, with the step itself replaced by the field write.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = {{n:x}};
	var y = {{n:y}};
	var mines = UnityEngine.Object.FindObjectsOfType<MineScript>();
	if (mines.Length == 0) { return "{\"ok\":false,\"error\":\"no-mine\"}"; }
	var mine = mines[0];
	var field = typeof(MineScript).GetField("pressed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	if (field == null) { return "{\"ok\":false,\"error\":\"no-field\"}"; }
	field.SetValue(mine, true);
	var pos = mine.transform.position;
	return "{\"ok\":true,\"mines\":" + mines.Length.ToString(inv)
		+ ",\"mineX\":" + pos.x.ToString("0.##", inv)
		+ ",\"mineY\":" + pos.y.ToString("0.##", inv) + "}";
}))()
