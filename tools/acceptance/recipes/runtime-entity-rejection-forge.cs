// recipe: runtime-entity-rejection-forge
// args: mode=s id=s cellx=n celly=n creator=s sequence=s
// serves: runtime-entity-creation-rejection rows 8 and 9
// returns: ok, mode, id, cellX, cellY, creator, sequence, error, detail
//
// The two forks the production wire cannot produce by itself, driven through the product's own entry
// points and then read through its own tables:
//   mode=foreign — fire ONE RuntimeEntityRejectedMsg at THIS client for the given key, as if the host
//     had answered it. A member that neither owns the creation token nor holds the pending report must
//     ignore it (row 8).
//   mode=report — hand-build ONE EntitySpawnedMsg whose creation token names ANOTHER member and send it
//     with the product's own SendEntitySpawned. The guest records the pending report before the send,
//     the host rejects the creation it cannot materialize, and the guest must act on the rejection
//     because its OWN pending table holds the key (row 9). The reported position is the given cell's
//     centre, so the key the host answers with is exactly the given cell.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var world = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.WorldService)) as CasualtiesUnknownOnline.Runtime.Session.World.WorldService;
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (world == null || session == null) { return "{\"ok\":false,\"error\":\"no-service\"}"; }
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var mode = {{s:mode}};
	var id = {{s:id}};
	var cellx = (int)({{n:cellx}});
	var celly = (int)({{n:celly}});
	var creator = System.Convert.ToUInt64({{s:creator}}, inv);
	var sequence = System.Convert.ToUInt32({{s:sequence}}, inv);
	if (mode == "foreign") {
		var host = session.HostSteamId;
		var key = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.RuntimeEntityKeyMsg();
		key.Id = id;
		key.CellX = cellx;
		key.CellY = celly;
		key.CreatorSteamId = creator;
		key.CreationSequence = sequence;
		var rejection = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.RuntimeEntityRejectedMsg();
		rejection.Key = key;
		rejection.Reason = CasualtiesUnknownOnline.Runtime.Protocol.Messages.RuntimeEntityRejectReason.PrefabUnavailable;
		world.FireRuntimeEntityRejectedReceived(host, rejection);
		return "{\"ok\":true,\"mode\":\"foreign\",\"id\":\"" + id + "\""
			+ ",\"cellX\":" + cellx.ToString(inv) + ",\"cellY\":" + celly.ToString(inv)
			+ ",\"creator\":\"" + creator.ToString(inv) + "\",\"sequence\":" + sequence.ToString(inv) + "}";
	}
	if (mode == "report") {
		var spawn = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.EntitySpawnedMsg();
		spawn.Id = id;
		spawn.Position = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg(cellx + 0.5f, celly + 0.5f);
		spawn.CreatorSteamId = creator;
		spawn.CreationSequence = sequence;
		world.SendEntitySpawned(spawn);
		return "{\"ok\":true,\"mode\":\"report\",\"id\":\"" + id + "\""
			+ ",\"cellX\":" + cellx.ToString(inv) + ",\"cellY\":" + celly.ToString(inv)
			+ ",\"creator\":\"" + creator.ToString(inv) + "\",\"sequence\":" + sequence.ToString(inv) + "}";
	}
	return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be foreign or report\"}";
}))()
