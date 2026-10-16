// recipe: member-identity-forge
// args: surface=s victim=s item=s
// serves: member-report-bounds A rows (a report that names another member's own body or hands)
// returns: ok, surface, victim, item, role, sessionActive, utc, error, detail
//
// Sends ONE report through the product's own sender on the MEMBER side, with the identity the payload
// carries set to ANOTHER member — the shape a modified client can send and no CUO gesture emits. Every
// surface here keys a durable write (or a replay) on that identity, so the host has to refuse it BY NAME
// on its own log and leave its record alone; the same sender with `victim` set to the caller's OWN id is
// the ordinary report and must still go through, which is what makes the reading a bound and not a
// blanket refusal. surface:
//   pickup  IItemControl's report path, hand-built as a WireCommand whose NewOwner is the other member
//           (item=<instance id of an item the host has ALREADY judged — an unjudged id is refused by the
//           creation-before-operation invariant before the identity rule is reached, which proves nothing)
//   bite|lunge|effect  EnemySyncService's own report, victim = the other member
//   limb    ICharacterDataControl.SendLimbStateEvent, OwnerSteamId = the other member
//   scene   the scene report itself, SteamId = the other member (Broadcast is the guest's only sender
//           that carries an id the product does not stamp)
//   speech  IWorldControl.SendSpeech, SpeakerSteamId = the other member
// GUEST-ONLY: every one of these report paths is the member's, so on a host this recipe refuses with
// not-guest instead of answering a false ok. `victim` is another member's decimal SteamId — read it
// from THAT member's own `item-census` (`localSteamId`), never derived here: the identity has to be a
// member the host keeps a record for, and the host keeps no presence row for itself. One eval.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var surface = {{s:surface}};
	var rawVictim = {{s:victim}};
	var rawItem = {{s:item}};
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
		return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"every report this recipe forges is the member's own path; run it on the guest\"}";
	}
	var victim = 0UL;
	if (!ulong.TryParse(rawVictim, System.Globalization.NumberStyles.None, inv, out victim) || victim == 0UL) {
		return "{\"ok\":false,\"error\":\"bad-victim\",\"detail\":\"victim must be the OTHER member's non-zero decimal SteamId — read it from that member's own item-census localSteamId\"}";
	}
	if (victim == session.LocalSteamId) {
		return "{\"ok\":false,\"error\":\"victim-is-self\",\"detail\":\"a report naming its own sender is the ordinary shape — this run would prove nothing\"}";
	}
	var item = 0UL;
	if (rawItem.Length != 0 && (!ulong.TryParse(rawItem, System.Globalization.NumberStyles.None, inv, out item))) {
		return "{\"ok\":false,\"error\":\"bad-item\",\"detail\":\"item must be a decimal item instance id (0 = not used by this surface)\"}";
	}
	var detail = "";
	try {
		if (surface == "pickup") {
			if (item == 0UL) { return "{\"ok\":false,\"error\":\"item-required\",\"detail\":\"the pickup shape needs item=<an id the host has already judged>\"}"; }
			var kernel = services.GetService(typeof(CasualtiesUnknownOnline.Application.Kernel.IKernelProtocolControl)) as CasualtiesUnknownOnline.Application.Kernel.IKernelProtocolControl;
			if (kernel == null) { return "{\"ok\":false,\"error\":\"no-kernel-protocol\"}"; }
			var command = new CasualtiesUnknownOnline.Protocol.Wire.WireCommand();
			command.Kind = CasualtiesUnknownOnline.Protocol.Wire.WireCommandKind.ItemPickup;
			command.Identity = new CasualtiesUnknownOnline.Protocol.Wire.WireItemIdentity();
			command.Identity.InstanceId = item;
			command.NewOwner = victim;
			command.ExpectedRevision = 0;
			kernel.SendCommand(command, CasualtiesUnknownOnline.Protocol.Wire.WirePayloadType.ItemPickupCommand);
		} else if (surface == "bite" || surface == "lunge" || surface == "effect") {
			var enemies = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.EntitySync.EnemySyncService)) as CasualtiesUnknownOnline.Runtime.Session.EntitySync.EnemySyncService;
			if (enemies == null) { return "{\"ok\":false,\"error\":\"no-enemy-sync\"}"; }
			var limb = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterLimbMsg();
			limb.Index = 0;
			if (surface == "bite") {
				var bite = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.EnemyBiteMsg();
				bite.VictimSteamId = victim;
				bite.Limb = limb;
				bite.VenomTotal = 1f;
				enemies.SendEnemyBite(bite);
			} else if (surface == "lunge") {
				var lunge = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.EnemyLungeMsg();
				lunge.VictimSteamId = victim;
				lunge.Limb = limb;
				lunge.Adrenaline = 1f;
				enemies.SendEnemyLunge(lunge);
			} else {
				var effect = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.EnemyEffectMsg();
				effect.VictimSteamId = victim;
				effect.Kind = CasualtiesUnknownOnline.Runtime.Protocol.EnemyEffectKind.GrabberGrabbed;
				effect.Shock = 1f;
				enemies.SendEnemyEffect(effect);
			}
		} else if (surface == "limb") {
			var characters = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.CharacterData.ICharacterDataControl)) as CasualtiesUnknownOnline.Runtime.Session.CharacterData.ICharacterDataControl;
			if (characters == null) { return "{\"ok\":false,\"error\":\"no-character-data\"}"; }
			var latch = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.LimbStateEventMsg();
			latch.OwnerSteamId = victim;
			var broken = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterLimbMsg();
			broken.Index = 0;
			broken.Broken = true;
			latch.Limbs.Add(broken);
			characters.SendLimbStateEvent(latch);
		} else if (surface == "scene") {
			var scene = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.SceneStateMsg();
			scene.State = (byte)CasualtiesUnknownOnline.Runtime.Session.SceneStateType.InWorld;
			scene.SceneName = "forge";
			scene.SteamId = victim;
			session.Broadcast(CasualtiesUnknownOnline.Runtime.Protocol.NetMsg.SceneState, scene);
		} else if (surface == "speech") {
			var world = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.IWorldControl)) as CasualtiesUnknownOnline.Runtime.Session.World.IWorldControl;
			if (world == null) { return "{\"ok\":false,\"error\":\"no-world-control\"}"; }
			var bubble = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.SpeechMsg();
			bubble.SpeakerSteamId = victim;
			bubble.Text = "forged bubble";
			world.SendSpeech(bubble);
		} else {
			return "{\"ok\":false,\"error\":\"bad-surface\",\"detail\":\"surface must be pickup, bite, lunge, effect, limb, scene or speech\"}";
		}
	}
	catch (System.Exception e) { detail = (e.GetType().Name + ": " + e.Message).Replace("\\", "/").Replace("\"", "'"); }
	if (detail.Length != 0) { return "{\"ok\":false,\"error\":\"send-failed\",\"detail\":\"" + detail + "\"}"; }
	return "{\"ok\":true,\"surface\":\"" + surface + "\""
		+ ",\"victim\":\"" + victim.ToString(inv) + "\""
		+ ",\"item\":\"" + item.ToString(inv) + "\""
		+ ",\"role\":\"Guest\""
		+ ",\"sessionActive\":" + (session.SessionActive ? "true" : "false")
		+ ",\"utc\":\"" + System.DateTime.UtcNow.ToString("o") + "\"}";
}))()
