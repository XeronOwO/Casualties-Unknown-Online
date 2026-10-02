// recipe: generation-forge
// args: mode=s stamp=s sender=s x=n y=n dmg=n block=n epoch=n layer=n id=s creator=s sequence=s drops=n
// serves: world-layer-generation-identity rows 5 and 8, generation-identity-remaining-families rows 1, 4 and 7
// returns: ok, mode, stamp, sender, generation, runEpoch, layerIndex, entries, error, detail
//
// Hands ONE hand-built message of the world/layer-generation families to THIS client's own receive seam
// (WorldService.Fire*Received / HandleBlockDamageReport), stamped either with this side's current
// baseline (stamp=current), with no stamp at all (stamp=none), or with an explicit other generation
// (stamp=stale, epoch/layer). The deployed peers always stamp and no frame can be held in flight across
// a descent on this machine, so the two shapes a verdict needs — an unstamped frame and a frame of a
// chosen other generation — are produced here through the product's own entry points; everything after
// the call (the gate's verdict, the log line, the materialization/refusal, the answer) is the deployed
// product's own code, read back from its tables, logs and censuses.
//
// Modes: block-placed | block-damaged | block-damage-report | entity-spawned | entity-snapshot |
//        entity-rejected | trap-layout.
// sender: local | host | peer | a decimal SteamId. id=auto takes the first live prefab id in this
// client's scene; for trap-layout the entry list is this client's OWN scanned layout plus one extra
// entry at (x,y), so an applied snapshot materializes exactly one copy and destroys nothing.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	var world = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.WorldService)) as CasualtiesUnknownOnline.Runtime.Session.World.WorldService;
	if (session == null || world == null) { return "{\"ok\":false,\"error\":\"no-service\"}"; }
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var stampArg = {{s:stamp}};
	var senderArg = {{s:sender}};
	var x = (int)({{n:x}});
	var y = (int)({{n:y}});
	var dmg = (float)({{n:dmg}});
	var block = (ushort)({{n:block}});
	var epoch = (ulong)({{n:epoch}});
	var layer = (int)({{n:layer}});
	var id = {{s:id}};
	var creator = System.Convert.ToUInt64({{s:creator}}, inv);
	var sequence = System.Convert.ToUInt32({{s:sequence}}, inv);
	var drops = (int)({{n:drops}});
	var sender = session.LocalSteamId;
	if (senderArg == "host") { sender = session.HostSteamId; }
	else if (senderArg == "peer") {
		foreach (var member in session.Members) { if (member.SteamId != session.LocalSteamId) { sender = member.SteamId; break; } }
		if (sender == session.LocalSteamId) { sender = session.HostSteamId; }
	}
	else if (senderArg != "local") { sender = System.Convert.ToUInt64(senderArg, inv); }
	CasualtiesUnknownOnline.Runtime.Protocol.Messages.WorldGenerationMsg generation = null;
	if (stampArg == "current") {
		var authority = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.ItemKernelAuthority));
		if (authority == null) { return "{\"ok\":false,\"error\":\"no-kernel-authority\"}"; }
		generation = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.WorldGenerationMsg();
		var epochProperty = authority.GetType().GetProperty("CurrentRunEpoch", flags);
		if (epochProperty != null) {
			var epochValue = epochProperty.GetValue(authority, null);
			if (epochValue != null) {
				var valueProperty = epochValue.GetType().GetProperty("Value", flags);
				if (valueProperty != null) { generation.RunEpoch = System.Convert.ToUInt64(valueProperty.GetValue(epochValue, null), inv); }
			}
		}
		var queryRun = authority.GetType().GetMethod("QueryRun", flags, null, new System.Type[0], null);
		if (queryRun != null) {
			var run = queryRun.Invoke(authority, null);
			if (run != null) {
				var layerProperty = run.GetType().GetProperty("LayerIndex", flags);
				if (layerProperty != null) { generation.LayerIndex = System.Convert.ToInt32(layerProperty.GetValue(run, null), inv); }
			}
		}
	}
	else if (stampArg == "stale") {
		generation = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.WorldGenerationMsg { RunEpoch = epoch, LayerIndex = layer };
	}
	else if (stampArg != "none") { return "{\"ok\":false,\"error\":\"bad-stamp\",\"detail\":\"stamp must be current, stale or none\"}"; }
	var prefabId = id;
	if ((mode == "entity-spawned" || mode == "entity-snapshot") && prefabId == "auto") {
		var all = UnityEngine.Object.FindObjectsOfType<BuildingEntity>(true);
		for (var i = 0; i < all.Length; i++) {
			var entity = all[i];
			if (entity == null) { continue; }
			var idField = entity.GetType().GetField("id", flags);
			var idValue = idField == null ? null : idField.GetValue(entity);
			if (idValue != null && System.Convert.ToString(idValue).Length > 0) { prefabId = System.Convert.ToString(idValue); break; }
		}
	}
	var reached = "{\"ok\":true,\"mode\":\"" + mode + "\""
		+ ",\"stamp\":\"" + stampArg + "\""
		+ ",\"sender\":\"" + sender.ToString(inv) + "\""
		+ ",\"generation\":" + (generation == null ? "null" : "\"" + generation.RunEpoch.ToString(inv) + "/" + generation.LayerIndex.ToString(inv) + "\"")
		+ ",\"runEpoch\":" + (generation == null ? "null" : generation.RunEpoch.ToString(inv))
		+ ",\"layerIndex\":" + (generation == null ? "null" : generation.LayerIndex.ToString(inv));
	if (mode == "block-placed") {
		world.FireBlockPlacedReceived(sender, x, y, block, false, generation);
		return reached + ",\"entries\":1}";
	}
	if (mode == "block-damaged") {
		System.Collections.Generic.List<CasualtiesUnknownOnline.Runtime.Protocol.Messages.BlockDropEntryMsg> dropList = null;
		if (drops > 0) {
			dropList = new System.Collections.Generic.List<CasualtiesUnknownOnline.Runtime.Protocol.Messages.BlockDropEntryMsg>();
			for (var i = 0; i < drops; i++) {
				var drop = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.BlockDropEntryMsg();
				drop.ItemId = 9100000000UL + (ulong)i;
				drop.Item = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg();
				drop.Position = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg(x + 0.5f, y + 0.5f);
				drop.Velocity = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg(0f, 0f);
				dropList.Add(drop);
			}
		}
		world.FireBlockDamagedReceived(sender, x, y, dmg, false, dropList, null, 0f, generation);
		return reached + ",\"entries\":1,\"drops\":" + drops.ToString(inv) + "}";
	}
	if (mode == "block-damage-report") {
		var rows = new System.Collections.Generic.List<CasualtiesUnknownOnline.Runtime.Protocol.Messages.BlockDamageEntryMsg>();
		rows.Add(new CasualtiesUnknownOnline.Runtime.Protocol.Messages.BlockDamageEntryMsg { X = x, Y = y, Damage = dmg });
		world.HandleBlockDamageReport(sender, rows, generation);
		return reached + ",\"entries\":1}";
	}
	if (mode == "entity-spawned" || mode == "entity-snapshot") {
		if (prefabId == "auto" || prefabId.Length == 0) { return "{\"ok\":false,\"error\":\"no-prefab-id\"}"; }
		var spawn = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.EntitySpawnedMsg();
		spawn.Id = prefabId;
		spawn.Position = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg(x + 0.5f, y + 0.5f);
		spawn.CreatorSteamId = creator;
		spawn.CreationSequence = sequence;
		spawn.Generation = generation;
		if (mode == "entity-spawned") {
			world.FireEntitySpawnedReceived(sender, spawn);
			return reached + ",\"entries\":1}";
		}
		var snapshot = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.RuntimeEntitySnapshotMsg { Generation = generation };
		snapshot.Entries.Add(spawn);
		world.FireRuntimeEntitySnapshotReceived(sender, snapshot);
		return reached + ",\"entries\":1}";
	}
	if (mode == "entity-rejected") {
		var key = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.RuntimeEntityKeyMsg();
		key.Id = id;
		key.CellX = x;
		key.CellY = y;
		key.CreatorSteamId = creator;
		key.CreationSequence = sequence;
		var rejection = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.RuntimeEntityRejectedMsg();
		rejection.Key = key;
		rejection.Reason = CasualtiesUnknownOnline.Runtime.Protocol.Messages.RuntimeEntityRejectReason.StaleGeneration;
		world.FireRuntimeEntityRejectedReceived(sender, rejection);
		return reached + ",\"entries\":1}";
	}
	if (mode == "trap-layout") {
		var entries = new System.Collections.Generic.List<CasualtiesUnknownOnline.Runtime.Protocol.Messages.TrapLayoutEntryMsg>();
		var scanType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.World.TrapEntityScan, CasualtiesUnknownOnline.GameAdapter", false);
		if (scanType != null) {
			var scanMethod = scanType.GetMethod("Scan", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			if (scanMethod != null) {
				var scanned = scanMethod.Invoke(null, null) as System.Collections.IEnumerable;
				if (scanned != null) {
					foreach (var item in scanned) {
						if (item == null) { continue; }
						var entryProperty = item.GetType().GetProperty("Entry", flags);
						if (entryProperty == null) { continue; }
						var entry = entryProperty.GetValue(item, null) as CasualtiesUnknownOnline.Runtime.Protocol.Messages.TrapLayoutEntryMsg;
						if (entry != null) { entries.Add(entry); }
					}
				}
			}
		}
		var extra = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.TrapLayoutEntryMsg();
		extra.Kind = entries.Count > 0 ? entries[0].Kind : default(CasualtiesUnknownOnline.Runtime.Protocol.EntityEventKind);
		extra.PrefabName = id == "auto" ? (entries.Count > 0 ? entries[0].PrefabName : string.Empty) : id;
		extra.X = x;
		extra.Y = y;
		entries.Add(extra);
		world.FireTrapLayoutReceived(sender, generation, entries);
		return reached + ",\"entries\":" + entries.Count.ToString(inv) + "}";
	}
	return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be block-placed, block-damaged, block-damage-report, entity-spawned, entity-snapshot, entity-rejected or trap-layout\"}";
}))()
