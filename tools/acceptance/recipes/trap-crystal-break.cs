// recipe: trap-crystal-break
// args: x=n y=n
// serves: member-report-bounds section C — the NetMsg.EntityEvent drop row's ordinary-event half: a REAL
//         fragile-crystal break whose rolled drops are judged on the host against the trap entity's own table
// returns: ok, x, y, foundX, foundY, distance, effect, effects, tableRows, healthBefore, healthAfter, role,
//          sessionActive, error, detail
//
// MEMBER ONLY, in an active session: the event report path is the guest's (`EntityEventSync.OnTrapTriggered`
// returns silently for a host or an inactive session), so the recipe refuses `not-guest` / `no-active-session`
// instead of breaking a crystal that reports nothing. Drives the game's own fragile-crystal break on the
// crystal nearest the absolute point (x,y), within the applier's own 3-unit tolerance: it calls the rolled
// CrystalFragile effect's `Touched` with the local BODY's GameObject — the same effect call
// `CrystalBehaviour.OnCollisionEnter2D` makes for a body's touch. The two scene guards that dispatch applies
// are checked here too (`generatingWorld`, and the closest chunk renderer being enabled), so the recipe
// refuses `world-generating` / `chunk-not-rendered` rather than breaking a crystal through a state where the
// game itself would not dispatch. What stays FORCED is the walk: the recipe supplies the touch a body would
// make, so a row read through it does not prove a player can walk into the crystal (the ticket's Limits say
// so).
//
// The dynamic Harmony postfix observes the call, the building-death branch rolls the drops natively on this
// side, and the product reports ONE event carrying the REAL rolled drops (TrapDropPendingState holds it two
// frames for the death branch + Item.Start). The recipe itself only proves the trigger landed
// (`healthAfter` below the death threshold, else `touch-not-applied`); the DROPS are read two frames later
// from the member's own log (`[TrapDropFlush] … drops=N`) and the host's (`[ItemSpawn] materializing …`).
//
// The target must carry a NON-EMPTY drop table: `tableRows` is `GameBuildingDropTable.ForEntity`'s row count
// for the crystal's building, read before the touch, and 0 rows answers `no-drop-table` — a tableless crystal
// would break with zero drops, nothing would be judged, and the row's "the rolled drops are judged" half
// would be skipped while this recipe answered ok. Pick the target from `trap-drop-census` (a fragile crystal
// with rows and `anchorOwn:true`).
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var esc = new System.Func<string, string>((value) => {
		if (value == null) { return ""; }
		var builder = new System.Text.StringBuilder();
		for (var i = 0; i < value.Length; i++) {
			var c = value[i];
			if (c == '\\' || c == '"') { builder.Append('\\').Append(c); }
			else if (c == '\n') { builder.Append("\\n"); }
			else if (c == '\r') { builder.Append("\\r"); }
			else if (c == '\t') { builder.Append("\\t"); }
			else if (c < ' ') { builder.Append(' '); }
			else { builder.Append(c); }
		}
		return builder.ToString();
	});
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session-control\"}"; }
	if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
		return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"the event report path is the member's; on the host this recipe would break a crystal that reports nothing\"}";
	}
	if (!session.SessionActive) { return "{\"ok\":false,\"error\":\"no-active-session\",\"detail\":\"the member's session is not active, so OnTrapTriggered would return without a report\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var all = UnityEngine.Object.FindObjectsOfType(typeof(CrystalBehaviour));
	if (all == null || all.Length == 0) { return "{\"ok\":false,\"error\":\"no-crystal\",\"detail\":\"no CrystalBehaviour is in this scene\"}"; }
	CrystalBehaviour target = null;
	var best = 3f;
	for (var i = 0; i < all.Length; i++) {
		var candidate = all[i] as CrystalBehaviour;
		if (candidate == null) { continue; }
		var distance = UnityEngine.Vector2.Distance(candidate.transform.position, new Vector2(x, y));
		if (distance < best) { target = candidate; best = distance; }
	}
	if (target == null) { return "{\"ok\":false,\"error\":\"no-crystal\",\"detail\":\"no CrystalBehaviour within 3 units of the point\"}"; }
	if (target.build == null) { return "{\"ok\":false,\"error\":\"no-building\",\"detail\":\"the crystal carries no BuildingEntity\"}"; }
	var pos = target.transform.position;
	var gameWorld = WorldGeneration.world;
	if (gameWorld == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	if (gameWorld.generatingWorld) { return "{\"ok\":false,\"error\":\"world-generating\",\"detail\":\"the game's own dispatch skips a touch while the world generates; the crystal was not touched\"}"; }
	var chunk = gameWorld.GetClosestChunkRenderer(gameWorld.WorldToBlockPos(new Vector2(pos.x, pos.y)));
	if (chunk == null || !chunk.enabled) { return "{\"ok\":false,\"error\":\"chunk-not-rendered\",\"detail\":\"the game's own dispatch skips a touch whose chunk renderer is off; the crystal was not touched\"}"; }
	var before = target.build.health;
	if (before < 0.5f) { return "{\"ok\":false,\"error\":\"already-dead\",\"detail\":\"the crystal at this position is already destroyed; a second report would be a duplicate\"}"; }
	var tableType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.World.GameBuildingDropTable, CasualtiesUnknownOnline.GameAdapter");
	var forEntity = tableType == null ? null : tableType.GetMethod("ForEntity", statics);
	if (forEntity == null) { return "{\"ok\":false,\"error\":\"no-drop-table-reader\",\"detail\":\"GameBuildingDropTable.ForEntity did not resolve, so the target's table cannot be read\"}"; }
	var tableRows = 0;
	var rows = forEntity.Invoke(null, new object[] { target.build }) as System.Collections.IEnumerable;
	if (rows != null) {
		foreach (var row in rows) {
			if (row != null) { tableRows = tableRows + 1; }
		}
	}
	if (tableRows == 0) {
		return "{\"ok\":false,\"error\":\"no-drop-table\",\"detail\":\"the crystal (" + esc(target.build.id) + ") has no authored drop rows, so its break would carry no drops and nothing would be judged; pick another target from trap-drop-census\"}";
	}
	var effectsField = typeof(CrystalBehaviour).GetField("effects", instanceFlags);
	var rolled = effectsField == null ? null : effectsField.GetValue(target) as System.Collections.IEnumerable;
	var names = new System.Collections.Generic.List<string>();
	object fragile = null;
	if (rolled != null) {
		foreach (var effect in rolled) {
			if (effect == null) { continue; }
			var effectName = effect.GetType().Name;
			names.Add(effectName);
			if (effectName == "CrystalFragile") { fragile = effect; }
		}
	}
	if (fragile == null) {
		return "{\"ok\":false,\"error\":\"no-fragile-effect\",\"detail\":\"this crystal rolled [" + esc(string.Join(",", names.ToArray())) + "], not CrystalFragile — pick another target\"}";
	}
	var touched = fragile.GetType().GetMethod("Touched", instanceFlags);
	if (touched == null) { return "{\"ok\":false,\"error\":\"no-touch-method\",\"detail\":\"the CrystalFragile type carries no Touched method\"}"; }
	var detail = "";
	try { touched.Invoke(fragile, new object[] { body.gameObject }); }
	catch (System.Exception e) {
		var inner = e is System.Reflection.TargetInvocationException && e.InnerException != null ? e.InnerException : e;
		detail = inner.GetType().Name + ": " + inner.Message;
	}
	if (detail.Length != 0) { return "{\"ok\":false,\"error\":\"invoke-failed\",\"detail\":\"" + esc(detail) + "\"}"; }
	var after = target.build.health;
	if (after >= 0.5f) {
		return "{\"ok\":false,\"error\":\"touch-not-applied\",\"detail\":\"the crystal's Touched returned but its health stayed at " + after.ToString("R", inv) + "; no break was reported\"}";
	}
	var effectsJson = new System.Text.StringBuilder();
	for (var n = 0; n < names.Count; n++) {
		if (n > 0) { effectsJson.Append(','); }
		effectsJson.Append('"').Append(esc(names[n])).Append('"');
	}
	return "{\"ok\":true"
		+ ",\"x\":" + x.ToString("R", inv)
		+ ",\"y\":" + y.ToString("R", inv)
		+ ",\"foundX\":" + pos.x.ToString("R", inv)
		+ ",\"foundY\":" + pos.y.ToString("R", inv)
		+ ",\"distance\":" + best.ToString("0.###", inv)
		+ ",\"effect\":\"CrystalFragile\""
		+ ",\"effects\":[" + effectsJson.ToString() + "]"
		+ ",\"tableRows\":" + tableRows.ToString(inv)
		+ ",\"healthBefore\":" + before.ToString("R", inv)
		+ ",\"healthAfter\":" + after.ToString("R", inv)
		+ ",\"role\":\"" + session.Role.ToString() + "\""
		+ ",\"sessionActive\":" + (session.SessionActive ? "true" : "false") + "}";
}))()
