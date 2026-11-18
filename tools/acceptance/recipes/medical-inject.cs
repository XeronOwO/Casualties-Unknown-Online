// recipe: medical-inject
// args: op=n ml=n count=n
// returns: ok, op, ml, count, total, error
//
// Drives an accepted remote medical operation's incremental ml the way the game's syringe
// minigame does: `IMedicalOperationControl.SendUpdate(operationId, deltaMl)` per increment,
// then `SendEndRequest(operationId, totalMl)`. The START is not staged here — the row makes it
// with the player's own release gesture (`remote-medical-release`), and this recipe only
// supplies the increments the minigame would send. The effect is the target's own client
// running the game's native `WaterContainerItem.Inject`.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var control = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IMedicalOperationControl))
		as CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IMedicalOperationControl;
	if (control == null) { return "{\"ok\":false,\"error\":\"no-medical-operations\"}"; }
	var op = {{n:op}};
	var ml = {{n:ml}};
	var count = {{n:count}};
	if (op <= 0) { return "{\"ok\":false,\"error\":\"bad-operation\",\"detail\":\"the operation id is not known\"}"; }
	if (ml <= 0f) { return "{\"ok\":false,\"error\":\"bad-ml\",\"detail\":\"ml must be positive\"}"; }
	if (count <= 0 || count > 60) { return "{\"ok\":false,\"error\":\"bad-count\",\"detail\":\"count must be 1..60\"}"; }
	for (var i = 0; i < count; i++) { control.SendUpdate(op, ml); }
	var total = ml * count;
	control.SendEndRequest(op, total);
	return "{\"ok\":true,\"op\":" + op.ToString(inv) + ",\"ml\":" + ml.ToString("0.###", inv)
		+ ",\"count\":" + count.ToString(inv) + ",\"total\":" + total.ToString("0.###", inv) + "}";
}))()
