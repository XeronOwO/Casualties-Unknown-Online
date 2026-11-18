// recipe: block-drop-forge
// args: dx=n dy=n absolute=n item=s condition=n count=n building=n airwrite=n item2=s condition2=n
// serves: validation-and-source-checks S2a rows 1-2 (a destruction report's drops are bounded) and S2b
//         (the building-death drops riding the same report are bounded against the dying buildings' tables)
// returns: ok, cellX, cellY, blockHere, item, condition, count, item2, building, airwrite, role, sessionActive, error, detail
//
// Reports ONE break of the cell dx/dy away from the local body (or, with absolute=1, of the ABSOLUTE cell
// dx,dy) through the product's own senders: the air write first — the shape the host records the destroyed
// block from — and then the drops-carrying break report with HAND-BUILT drops (one definition id at one
// condition, repeated `count` times), which is the report a modified client can send and no CUO gesture
// emits. With building=1 the drops ride as BUILDING-DEATH drops (TrapDropEntryMsg) instead of block drops,
// which is the half the host bounds against the prefab tables of the buildings that lost their support block
// at that cell — a shape the host accepts only when it marked one there, so this is also the probe for the
// mismatch refusal. The game's own roll is deliberately NOT run (that is `block-break-at`): this recipe
// isolates the bound, so the reported drop is exactly what the caller names and the host has to decide it
// from its own record of the cell. The relative form is unusable while the body is still falling (the offset
// resolves to a different cell every read), which is what the absolute form is for.
//
// airwrite=0 sends the drops-carrying report ALONE, which is the shape the breaker's own fallback re-sends
// when its relay (the acknowledgement) was the lost message: the cell is already air here, so the local
// SetBlock(0) never runs again and no fresh air write goes out. On the host that report is judged against
// the record the cell's ACCEPTED break already carries — including the drop tables captured when that break
// marked the buildings that lost support there — so this is the switch that reads the captured-rows half on
// a cell whose buildings are long destroyed. The re-report carries zero damage, exactly as the breaker's
// own fallback re-send does. airwrite=1 (the default shape, and what every earlier run used) sends the air
// write first, and the report carries the 100-damage hit.
//
// item2 (with condition2) adds ONE more drop to the same building-death report, so a single verdict reads
// both directions of the bound at once: the drop the captured rows can account for keeps its normal fate
// while the drop no row can produce is refused by name — the per-drop partition, on the real path. Leave
// item2 empty for the single-drop shape.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var world = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.IWorldControl)) as CasualtiesUnknownOnline.Runtime.Session.World.IWorldControl;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world-control\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	var gameWorld = WorldGeneration.world;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (gameWorld == null || body == null) { return "{\"ok\":false,\"error\":\"no-world-or-body\"}"; }
	var dx = (int)({{n:dx}});
	var dy = (int)({{n:dy}});
	var absolute = (int)({{n:absolute}});
	var itemId = {{s:item}};
	var condition = (float)({{n:condition}});
	var count = (int)({{n:count}});
	var building = (int)({{n:building}});
	var airwrite = (int)({{n:airwrite}});
	var origin = absolute != 0 ? new Vector2Int(dx, dy) : gameWorld.WorldToBlockPos(body.transform.position) + new Vector2Int(dx, dy);
	var cell = origin;
	var blockHere = (int)gameWorld.GetBlock(cell);
	if (airwrite != 0) {
		world.SendBlockPlacedReport(cell.x, cell.y, 0, true);
	}
	// The re-report carries ZERO damage: the fallback re-sends the break claim, not the hit (the
	// arbitration record's own words, "only a fallback's re-send carries zero").
	var dmg = airwrite != 0 ? 100f : 0f;
	if (building != 0) {
		// The building-death half: the same hand-built drop, riding the same break report as a
		// TrapDropEntryMsg. Its instance ids are offset from the block half's so a run that reads both can
		// never have one answer the other. item2 (one extra drop, at condition2) is what lets ONE report
		// carry both directions of the bound at once — the drop the captured rows account for and the drop
		// no row can produce — so the per-drop partition is read from one verdict. One call therefore names
		// each of its ids once, and two calls that must not answer for each other belong at different cells.
		var item2Id = {{s:item2}};
		var condition2 = (float)({{n:condition2}});
		var carried = count + (item2Id.Length > 0 ? 1 : 0);
		var bdrops = new System.Collections.Generic.List<CasualtiesUnknownOnline.Runtime.Protocol.Messages.TrapDropEntryMsg>();
		for (var i = 0; i < carried; i++) {
			var bdrop = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.TrapDropEntryMsg();
			bdrop.ItemId = 910000000000UL + ((ulong)(cell.x & 0xFFFF) * 65536UL) + (ulong)(cell.y & 0xFFFF) * 16UL + (ulong)i;
			bdrop.Item = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg();
			bdrop.Item.InstanceId = bdrop.ItemId;
			bdrop.Item.ItemId = i < count ? itemId : item2Id;
			bdrop.Item.Condition = i < count ? condition : condition2;
			bdrop.Position = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg();
			bdrop.Position.X = cell.x;
			bdrop.Position.Y = cell.y;
			bdrop.Velocity = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg();
			bdrops.Add(bdrop);
		}
		world.SendBlockDamaged(cell.x, cell.y, dmg, false, 0f, null, bdrops);
		return "{\"ok\":true"
			+ ",\"cellX\":" + cell.x.ToString(inv)
			+ ",\"cellY\":" + cell.y.ToString(inv)
			+ ",\"blockHere\":" + blockHere.ToString(inv)
			+ ",\"item\":\"" + itemId + "\""
			+ ",\"condition\":" + condition.ToString("R", inv)
			+ ",\"count\":" + carried.ToString(inv)
			+ ",\"item2\":\"" + item2Id + "\""
			+ ",\"building\":1"
			+ ",\"airwrite\":" + airwrite.ToString(inv)
			+ ",\"role\":\"" + (session == null ? "?" : session.Role.ToString()) + "\""
			+ ",\"sessionActive\":" + (session != null && session.SessionActive ? "true" : "false") + "}";
	}
	var drops = new System.Collections.Generic.List<CasualtiesUnknownOnline.Runtime.Protocol.Messages.BlockDropEntryMsg>();
	for (var i = 0; i < count; i++) {
		var drop = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.BlockDropEntryMsg();
		// One instance id per CELL and index, never one per recipe: two calls that name the same id would
		// make a later refusal answer the earlier accepted drop (the breaker destroys the copy that id
		// names), which is a run artifact rather than a reading.
		drop.ItemId = 900000000000UL + ((ulong)(cell.x & 0xFFFF) * 65536UL) + (ulong)(cell.y & 0xFFFF) * 16UL + (ulong)i;
		drop.Item = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg();
		drop.Item.InstanceId = drop.ItemId;
		drop.Item.ItemId = itemId;
		drop.Item.Condition = condition;
		drop.Position = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg();
		drop.Position.X = cell.x;
		drop.Position.Y = cell.y;
		drop.Velocity = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg();
		drops.Add(drop);
	}
	world.SendBlockDamaged(cell.x, cell.y, dmg, false, 0f, drops, null);
	return "{\"ok\":true"
		+ ",\"cellX\":" + cell.x.ToString(inv)
		+ ",\"cellY\":" + cell.y.ToString(inv)
		+ ",\"blockHere\":" + blockHere.ToString(inv)
		+ ",\"item\":\"" + itemId + "\""
		+ ",\"condition\":" + condition.ToString("R", inv)
		+ ",\"count\":" + count.ToString(inv)
		+ ",\"airwrite\":" + airwrite.ToString(inv)
		+ ",\"role\":\"" + (session == null ? "?" : session.Role.ToString()) + "\""
		+ ",\"sessionActive\":" + (session != null && session.SessionActive ? "true" : "false") + "}";
}))()
