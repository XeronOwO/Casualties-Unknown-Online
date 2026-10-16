// recipe: radiation-line
// args: mode=s value=n
// returns: ok, mode, active, timeGone, positionY, halfHeight, height, clearance, error, detail
//
// The radiation line, as this client sees it. `mode=read` answers what the line is doing HERE — its active
// flag, its descent (`timeGone`), its world position and how far that position sits above the layer floor —
// and `mode=activate` / `mode=set` drive it so a run can stand the line at a chosen depth instead of waiting
// out a layer's own clock (`WorldGeneration.Update` arms it when `layerTimeSpent > maxTimePerLayer`, which is
// minutes). `mode=set` writes the SAME private field the game advances, through the field's own type.
//
// The reading that matters for the host rule is `clearance`: the line's height above the floor is
// `halfHeight - timeGone` (plus the native wave), so a clamped line reports the margin the rule parks it at,
// and the same number on every client is what "the rule is the same everywhere" means.
//
// No inner lambda captures a local: the evaluator's Mono REPL refuses that shape.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var value = {{n:value}};
	var culture = System.Globalization.CultureInfo.InvariantCulture;
	var line = RadiationLine.line;
	if (line == null) { return "{\"ok\":false,\"error\":\"no-line\"}"; }

	var world = WorldGeneration.world;
	var field = typeof(RadiationLine).GetField("timeGone", instanceFlags);
	if (field == null) { return "{\"ok\":false,\"error\":\"no-timeGone-field\"}"; }

	if (mode == "activate") { line.Activate(); }
	else if (mode == "set") { field.SetValue(line, value); }
	else if (mode != "read") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be read, activate or set\"}"; }

	var timeGone = System.Convert.ToSingle(field.GetValue(line), culture);
	var halfHeight = world != null ? (float)world.halfHeight : 0f;
	var height = world != null ? (float)world.height : 0f;
	var positionY = line.transform.position.y;
	// The line sits at `halfHeight - timeGone` with the native wave on top of it; the clearance is how much
	// room the floor still has under it.
	var clearance = positionY + halfHeight;
	return "{\"ok\":true,\"mode\":\"" + mode + "\""
		+ ",\"active\":" + (line.active ? "true" : "false")
		+ ",\"timeGone\":" + timeGone.ToString("0.###", culture)
		+ ",\"positionY\":" + positionY.ToString("0.###", culture)
		+ ",\"halfHeight\":" + halfHeight.ToString("0.###", culture)
		+ ",\"height\":" + height.ToString("0.###", culture)
		+ ",\"clearance\":" + clearance.ToString("0.###", culture) + "}";
}))()
