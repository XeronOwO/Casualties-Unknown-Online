// recipe: block-drop-forge
// args: dx=n dy=n absolute=n item=s condition=n count=n building=n
// serves: validation-and-source-checks S2a rows 1-2 (a destruction report's drops are bounded) and S2b
//         (the building-death drops riding the same report are bounded against the dying buildings' tables)
// returns: ok, cellX, cellY, blockHere, item, condition, count, building, role, sessionActive, error, detail
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
	var origin = absolute != 0 ? new Vector2Int(dx, dy) : gameWorld.WorldToBlockPos(body.transform.position) + new Vector2Int(dx, dy);
	var cell = origin;
	var blockHere = (int)gameWorld.GetBlock(cell);
	world.SendBlockPlacedReport(cell.x, cell.y, 0, true);
	if (building != 0) {
		// The building-death half: the same hand-built drop, riding the same break report as a
		// TrapDropEntryMsg. Its instance ids are offset from the block half's so a run that reads both can
		// never have one answer the other.
		var bdrops = new System.Collections.Generic.List<CasualtiesUnknownOnline.Runtime.Protocol.Messages.TrapDropEntryMsg>();
		for (var i = 0; i < count; i++) {
			var bdrop = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.TrapDropEntryMsg();
			bdrop.ItemId = 910000000000UL + ((ulong)(cell.x & 0xFFFF) * 65536UL) + (ulong)(cell.y & 0xFFFF) * 16UL + (ulong)i;
			bdrop.Item = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg();
			bdrop.Item.InstanceId = bdrop.ItemId;
			bdrop.Item.ItemId = itemId;
			bdrop.Item.Condition = condition;
			bdrop.Position = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg();
			bdrop.Position.X = cell.x;
			bdrop.Position.Y = cell.y;
			bdrop.Velocity = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg();
			bdrops.Add(bdrop);
		}
		world.SendBlockDamaged(cell.x, cell.y, 100f, false, 0f, null, bdrops);
		return "{\"ok\":true"
			+ ",\"cellX\":" + cell.x.ToString(inv)
			+ ",\"cellY\":" + cell.y.ToString(inv)
			+ ",\"blockHere\":" + blockHere.ToString(inv)
			+ ",\"item\":\"" + itemId + "\""
			+ ",\"condition\":" + condition.ToString("R", inv)
			+ ",\"count\":" + count.ToString(inv)
			+ ",\"building\":1"
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
	world.SendBlockDamaged(cell.x, cell.y, 100f, false, 0f, drops, null);
	return "{\"ok\":true"
		+ ",\"cellX\":" + cell.x.ToString(inv)
		+ ",\"cellY\":" + cell.y.ToString(inv)
		+ ",\"blockHere\":" + blockHere.ToString(inv)
		+ ",\"item\":\"" + itemId + "\""
		+ ",\"condition\":" + condition.ToString("R", inv)
		+ ",\"count\":" + count.ToString(inv)
		+ ",\"role\":\"" + (session == null ? "?" : session.Role.ToString()) + "\""
		+ ",\"sessionActive\":" + (session != null && session.SessionActive ? "true" : "false") + "}";
}))()
