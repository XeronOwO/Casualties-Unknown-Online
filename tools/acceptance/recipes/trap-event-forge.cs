// recipe: trap-event-forge
// args: kind=s x=n y=n item=s condition=n id=s
// serves: member-report-bounds section C — the NetMsg.EntityEvent drop row's REFUSAL half: the lever no
//         acceptance recipe had, a hand-built drop list inside a real EntityEventMsg sent by a member
// returns: ok, kind, x, y, item, condition, id, destructive, role, sessionActive, error, detail
//
// Publishes ONE EntityEventMsg as this client through the product's own sender (IWorldControl.SendEntityEvent
// — the same report path a trap trigger takes), with ONE hand-built TrapDropEntryMsg: the claim a modified
// client can send and no CUO gesture emits. The host applies the event to its own world (the trigger is the
// reporter's fact and stays unbounded — decision 265) and judges the RIDING drops against the tables of the
// entities its own application destroyed or damaged plus the trap entity's own table at the position.
//
// THIS RECIPE IS FIRE-AND-FORGET: `ok:true` means the send call returned, never that the host judged the
// drop. The refusal and the reporter's destroy land in TWO DIFFERENT LOGS (the clients do not share a log
// tree), so the row is read from the chain, in this order — and only the SECOND line is the row's:
//   1. host:   `[TrapEvent] kind=… pos=(…) origin=HostApply from <member>.`            (the report arrived)
//   2. host:   `[WRN] [TrapDrop] <member>'s <kind> at (…) claimed 1 drop(s) the N row(s) of the entities
//              this host's own application destroyed or damaged cannot produce — refused by name
//              (<item>@<cond>), 0 drop(s) accepted and relayed.`   ← the TABLE arm, the only one this row is.
//   3. member: `Kernel command rejected by host for item <id>: ItemSpawn refused (TrapDropUnaccounted).`
//   4. member: `[ItemReject] refused drop <id> (<type>) — destroying the local copy (TrapDropUnaccounted).`
// The judge has TWO OTHER arms that answer with the SAME reason and are NOT this row: the empty-bounds arm
// (`…destroyed or damaged no entity with a drop table — all refused by name…`, which fires when the target
// has no table or the host's position lookup finds no entity — the anchor was never read) and the
// non-destructive-kind arm (`…outside the building-death carrier's profile…`). The recipe refuses
// `not-destructive` for exactly that reason: the caller passes one of the four carrier kinds
// (`MineExploded`, `TurretSelfDestructed`, `CrystalFragileBroken`, `CrystalUnstableExploded`, read from
// `TrapDamageProfiles` itself), and the run confirms the refusal came from the TABLE arm by the line's
// `N row(s) … cannot produce` shape.
//
// `<id>` must be the instance id of a REAL WORLD item on this client (checked before sending, `no-world-item`
// otherwise): the answer path finds the item by that id, so a made-up id reads the host's refusal but the
// reporter destroys nothing — a half-reading. The run creates one first (`item-drop`, then `item-world-read`
// for its id) on the MEMBER client, and `<item>` should be a definition NO row of the target's table names
// (`trap-drop-census` returns the rows). GUEST-ONLY: the host branch of the sender records and broadcasts a
// locally-applied event and never reaches the judge, so a host invocation refuses `not-guest`. One eval.
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
	var kindName = {{s:kind}};
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var itemId = {{s:item}};
	var condition = (float)({{n:condition}});
	var idText = {{s:id}};
	var instanceId = 0UL;
	if (!ulong.TryParse(idText, System.Globalization.NumberStyles.None, inv, out instanceId) || instanceId == 0UL) {
		return "{\"ok\":false,\"error\":\"bad-id\",\"detail\":\"id must be the decimal instance id of a real world item on this client (create one with item-drop, read it with item-world-read)\"}";
	}
	if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)) {
		return "{\"ok\":false,\"error\":\"bad-position\",\"detail\":\"the event position must be finite\"}";
	}
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var world = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.IWorldControl)) as CasualtiesUnknownOnline.Runtime.Session.World.IWorldControl;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world-control\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session-control\"}"; }
	if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
		return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"the report path is guest-only; run this on the member, not the host\"}";
	}
	if (!session.SessionActive) { return "{\"ok\":false,\"error\":\"no-active-session\",\"detail\":\"the member's session is not active, so nothing would be sent\"}"; }
	var kindValue = default(CasualtiesUnknownOnline.Runtime.Protocol.EntityEventKind);
	var kindParsed = false;
	try {
		kindValue = (CasualtiesUnknownOnline.Runtime.Protocol.EntityEventKind)System.Enum.Parse(typeof(CasualtiesUnknownOnline.Runtime.Protocol.EntityEventKind), kindName, true);
		kindParsed = true;
	}
	catch (System.Exception) { kindParsed = false; }
	if (!kindParsed) {
		return "{\"ok\":false,\"error\":\"bad-kind\",\"detail\":\"'" + esc(kindName) + "' is not an EntityEventKind name\"}";
	}
	var profilesType = System.Type.GetType("CasualtiesUnknownOnline.Runtime.Session.World.TrapDamageProfiles, CasualtiesUnknownOnline.Runtime");
	var isDestructive = profilesType == null ? null : profilesType.GetMethod("IsDestructive", statics);
	if (isDestructive == null) { return "{\"ok\":false,\"error\":\"no-carrier-profile\",\"detail\":\"TrapDamageProfiles.IsDestructive did not resolve, so the kind cannot be checked against the drop carrier\"}"; }
	var destructive = System.Convert.ToBoolean(isDestructive.Invoke(null, new object[] { kindValue }));
	if (!destructive) {
		return "{\"ok\":false,\"error\":\"not-destructive\",\"detail\":\"'" + esc(kindName) + "' is outside the building-death carrier's profile; its drops would be refused by the OTHER arm (every drop, same reason), not by the entity table this row reads\"}";
	}
	var carriesItem = false;
	var worldItems = UnityEngine.Object.FindObjectsOfType(typeof(Item));
	for (var i = 0; i < worldItems.Length; i++) {
		var candidate = worldItems[i] as Item;
		if (candidate == null || candidate.GetComponentInParent<Body>() != null) { continue; }
		var components = candidate.GetComponents<UnityEngine.Component>();
		for (var c = 0; c < components.Length; c++) {
			if (components[c] == null || components[c].GetType().Name != "ItemInstanceId") { continue; }
			// The id is a PROPERTY on the component (its field spelling differs), so the property is read
			// first and the field is only the fallback — the reverse reads 0 for a stamped id.
			var idProperty = components[c].GetType().GetProperty("Id", instanceFlags);
			var idField = components[c].GetType().GetField("Id", instanceFlags);
			var candidateId = 0UL;
			if (idProperty != null) { candidateId = System.Convert.ToUInt64(idProperty.GetValue(components[c], null)); }
			else if (idField != null) { candidateId = System.Convert.ToUInt64(idField.GetValue(components[c])); }
			if (candidateId == instanceId) { carriesItem = true; }
			break;
		}
		if (carriesItem) { break; }
	}
	if (!carriesItem) {
		return "{\"ok\":false,\"error\":\"no-world-item\",\"detail\":\"no world item carries id " + instanceId.ToString(inv) + " on this client, so the refusal would have nothing to destroy inside\"}";
	}
	var msg = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.EntityEventMsg();
	msg.Kind = kindValue;
	msg.Position = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg(x, y);
	var drop = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.TrapDropEntryMsg();
	drop.ItemId = instanceId;
	drop.Item = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg();
	drop.Item.InstanceId = instanceId;
	drop.Item.ItemId = itemId;
	drop.Item.Condition = condition;
	drop.Position = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg(x, y);
	drop.Velocity = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.NetVector2Msg();
	drop.Rotation = 0f;
	drop.FreshItemDrop = false;
	drop.AngularVelocity = 0f;
	msg.Drops.Add(drop);
	var detail = "";
	try { world.SendEntityEvent(msg); }
	catch (System.Exception e) { detail = e.GetType().Name + ": " + e.Message; }
	if (detail.Length != 0) { return "{\"ok\":false,\"error\":\"send-failed\",\"detail\":\"" + esc(detail) + "\"}"; }
	return "{\"ok\":true"
		+ ",\"kind\":\"" + esc(kindName) + "\""
		+ ",\"x\":" + x.ToString("R", inv)
		+ ",\"y\":" + y.ToString("R", inv)
		+ ",\"item\":\"" + esc(itemId) + "\""
		+ ",\"condition\":" + condition.ToString("R", inv)
		+ ",\"id\":" + instanceId.ToString(inv)
		+ ",\"destructive\":true"
		+ ",\"role\":\"" + session.Role.ToString() + "\""
		+ ",\"sessionActive\":" + (session.SessionActive ? "true" : "false") + "}";
}))()
