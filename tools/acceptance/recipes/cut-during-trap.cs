// recipe: cut-during-trap
// args: x=n y=n
// serves: save-mid-run-consistent-cut row 4 (an in-flight state must resolve before the cut)
// returns: ok, mines, mineX, mineY, asked, refusal, error, detail
//
// Presses the mine nearest the given point (`MineScript.pressed`, the field the player's step sets) and asks the save
// service for a cut IN THE SAME CALL — the one way to land inside the deferral window: the trap's drop hold is a
// `TrapDropPendingState` with the ResolveBeforeSave verdict, so the cut must wait for it and the host log names it.
// The request goes through the product's own `IWorldSaveControl.TryRequestCut`, the call the /save verb makes.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = {{n:x}};
	var y = {{n:y}};
	var mines = UnityEngine.Object.FindObjectsOfType<MineScript>();
	if (mines.Length == 0) { return "{\"ok\":false,\"error\":\"no-mine\"}"; }
	var mine = mines[0];
	var field = typeof(MineScript).GetField("pressed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	if (field == null) { return "{\"ok\":false,\"error\":\"no-field\"}"; }
	field.SetValue(mine, true);
	var pos = mine.transform.position;

	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var asms = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type controlType = null;
	System.Type reasonType = null;
	for (var i = 0; i < asms.Length; i++) {
		if (controlType == null) { try { controlType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Session.Persistence.IWorldSaveControl", false); } catch { controlType = null; } }
		if (reasonType == null) { try { reasonType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Persistence.WorldCutReason", false); } catch { reasonType = null; } }
		if (controlType != null && reasonType != null) { break; }
	}
	if (controlType == null) { return "{\"ok\":false,\"error\":\"no-control-type\"}"; }
	if (reasonType == null) { return "{\"ok\":false,\"error\":\"no-reason-type\"}"; }
	var control = services.GetService(controlType);
	if (control == null) { return "{\"ok\":false,\"error\":\"no-save-control\"}"; }
	var reason = System.Enum.Parse(reasonType, "Command");
	var args = new object[] { reason, null };
	var asked = (bool)controlType.GetMethod("TryRequestCut").Invoke(control, args);
	return "{\"ok\":true,\"mines\":" + mines.Length.ToString(inv)
		+ ",\"mineX\":" + pos.x.ToString("0.##", inv)
		+ ",\"mineY\":" + pos.y.ToString("0.##", inv)
		+ ",\"asked\":" + (asked ? "true" : "false")
		+ ",\"refusal\":" + (args[1] == null ? "null" : "\"" + args[1].ToString().Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"") + "}";
}))()
