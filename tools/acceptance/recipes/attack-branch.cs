// recipe: attack-branch
// args: none
// returns: ok, bind, stagedHeld, stagedDown, liquid, item, usableWithLmb, autoAttack, allowUseItem, conscious
//
// Replays every condition on the path from the held key to `Body.UseItemInHand` (`PlayerCamera.cs:1995-2014` and
// `Body.cs:2718-2732`) and reports the staged answer the game's own probe would give for the attack key. It exists
// because "the game entered HandleAttacks 706 times and still never swung" cannot be told apart from "the branch was
// taken and the body refused" without asking each gate by name.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var bind = KeyBinds.GetBind("attack");
	var stagedHeld = false;
	var stagedDown = false;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var a = 0; a < assemblies.Length; a++)
	{
		System.Type[] types;
		try { types = assemblies[a].GetTypes(); } catch { continue; }
		for (var t = 0; t < types.Length; t++)
		{
			if (types[t].Name != "VirtualInput") { continue; }
			var answer = types[t].GetMethod("TryAnswerKey", flags);
			if (answer == null) { continue; }
			var args = new object[] { bind, null, false };
			var heldType = types[t].GetNestedType("ButtonQuery", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			if (heldType == null || !heldType.IsEnum) { continue; }
			args[1] = System.Enum.Parse(heldType, "Held");
			var answered = (bool)answer.Invoke(null, args);
			stagedHeld = answered && (bool)args[2];
			args[2] = false;
			args[1] = System.Enum.Parse(heldType, "Down");
			var downAnswered = (bool)answer.Invoke(null, args);
			stagedDown = downAnswered && (bool)args[2];
		}
	}
	var item = camera.body.GetItem(camera.body.handSlot);
	var liquid = LiquidTransfer.instance != null;
	var atEntry = "unknown";
	for (var a = 0; a < assemblies.Length; a++)
	{
		System.Type[] types;
		try { types = assemblies[a].GetTypes(); } catch { continue; }
		for (var t = 0; t < types.Length; t++)
		{
			if (types[t].Name != "VirtualInput") { continue; }
			var gates = types[t].GetProperty("AttackGates", flags);
			if (gates != null)
			{
				var value = gates.GetValue(null, null);
				atEntry = value == null ? "(null)" : value.ToString();
			}
		}
	}
	return "{\"ok\":true"
		+ ",\"atEntry\":\"" + atEntry + "\""
		+ ",\"bind\":\"" + bind.ToString() + "\""
		+ ",\"stagedHeld\":" + (stagedHeld ? "true" : "false")
		+ ",\"stagedDown\":" + (stagedDown ? "true" : "false")
		+ ",\"liquid\":" + (liquid ? "true" : "false")
		+ ",\"item\":\"" + (item == null ? "" : item.name) + "\""
		+ ",\"usableWithLmb\":" + (item == null ? "false" : (item.Stats.usableWithLMB ? "true" : "false"))
		+ ",\"autoAttack\":" + (item == null ? "false" : (item.Stats.autoAttack ? "true" : "false"))
		+ ",\"allowUseItem\":" + (camera.body.allowUseItem ? "true" : "false")
		+ ",\"conscious\":" + (camera.body.conscious ? "true" : "false") + "}";
}))()
