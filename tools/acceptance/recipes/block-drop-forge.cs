// recipe: block-drop-forge
// args: dx=n dy=n item=s condition=n count=n
// serves: validation-and-source-checks S2a rows 1-2 (a destruction report's drops are bounded)
// returns: ok, cellX, cellY, blockHere, item, condition, count, role, sessionActive, error, detail
//
// Reports ONE break of the cell dx/dy away from the local body through the product's own senders: the air
// write first — the shape the host records the destroyed block from — and then the drops-carrying break
// report with HAND-BUILT drops (one definition id at one condition, repeated `count` times), which is the
// report a modified client can send and no CUO gesture emits. The game's own roll is deliberately NOT run
// (that is `block-break-at`): this recipe isolates the bound, so the reported drop is exactly what the
// caller names and the host has to decide it from its own record of the cell.
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
	var itemId = {{s:item}};
	var condition = (float)({{n:condition}});
	var count = (int)({{n:count}});
	var origin = gameWorld.WorldToBlockPos(body.transform.position);
	var cell = new Vector2Int(origin.x + dx, origin.y + dy);
	var blockHere = (int)gameWorld.GetBlock(cell);
	world.SendBlockPlacedReport(cell.x, cell.y, 0, true);
	var drops = new System.Collections.Generic.List<CasualtiesUnknownOnline.Runtime.Protocol.Messages.BlockDropEntryMsg>();
	for (var i = 0; i < count; i++) {
		var drop = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.BlockDropEntryMsg();
		drop.ItemId = 900000000000UL + (ulong)i;
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
