// recipe: close-wound-view
// args: none
// serves: pause-clock, world-clock, every row that follows a medical, craft or trade row
// returns: ok, closed, woundView, crafting, trade, error, detail
//
// Closes the game's own medical, craft and trade panels through the entries a player's own keys run, so the next row
// meets the game with no panel in front of its keys. Two facts decide this shape:
//
// 1. The medical panel our rows open is the game's native WoundView: `medical-open` calls CUO's
//    `IRemoteMedicalPresentation.OpenRemoteMedical`, which activates `PlayerCamera.main.woundView`
//    (src/CasualtiesUnknownOnline.GameAdapter/Character/RemoteMedicalView.cs), and the game's own pause key chain
//    takes the key for an open wound view before it pauses (PlayerCamera.cs:986-1020 — read in the gates
//    `tools/acceptance/recipes/pause-state.cs` returns).
// 2. CUO's close path is `RemoteMedicalView.Close()`: the same call the panel key ends in, which toggles the native
//    wound view off, releases the display-only body and gives the local-only action surfaces back. Calling
//    `PlayerCamera.ToggleWoundView` alone would leave that cleanup behind, and calling it on a CLOSED panel would OPEN
//    it (the method toggles), so every branch below first proves the panel is actually open.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => {
	var findType = new System.Func<string, System.Type>((simpleName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } }
		}
		return null;
	});
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var camera = PlayerCamera.main;
	if (camera == null)
	{
		// No camera means no world on this client, and a panel cannot be open without one: the menu the fresh-run
		// rows start from is the pair's own waiting room, not a leftover to undo.
		return "{\"ok\":true,\"closed\":\"\",\"world\":false}";
	}
	var field = new System.Func<string, object>((name) => {
		var info = camera.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		return info == null ? null : info.GetValue(camera);
	});
	var isOpen = new System.Func<string, bool>((name) => field(name) is UnityEngine.GameObject panel && panel != null && panel.activeSelf);
	var closed = new System.Collections.Generic.List<string>();
	var problems = new System.Collections.Generic.List<string>();
	try
	{
		if (isOpen("woundView"))
		{
			var view = findType("RemoteMedicalView");
			var close = view == null ? null : view.GetMethod("Close", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			if (close != null)
			{
				close.Invoke(null, null);
			}
			else
			{
				camera.GetType().GetMethod("ToggleWoundView", Flags).Invoke(camera, new object[] { false });
			}
			closed.Add("woundview");
		}
	}
	catch (System.Exception error) { problems.Add("woundview: " + (error.InnerException ?? error).Message); }
	try
	{
		if (isOpen("craftingPanel"))
		{
			camera.GetType().GetMethod("OpenCraftScreen", Flags).Invoke(camera, null);
			closed.Add("crafting");
		}
	}
	catch (System.Exception error) { problems.Add("crafting: " + (error.InnerException ?? error).Message); }
	try
	{
		if (isOpen("tradeMenu"))
		{
			camera.GetType().GetMethod("ToggleTradeMenu", Flags).Invoke(camera, null);
			closed.Add("trade");
		}
	}
	catch (System.Exception error) { problems.Add("trade: " + (error.InnerException ?? error).Message); }
	if (problems.Count > 0)
	{
		return "{\"ok\":false,\"error\":\"panel-close-failed\",\"detail\":\"" + string.Join("; ", problems).Replace("\\", "/").Replace("\"", "''") + "\"}";
	}
	return "{\"ok\":true,\"closed\":\"" + string.Join(",", closed) + "\""
		+ ",\"woundView\":" + (isOpen("woundView") ? "true" : "false")
		+ ",\"crafting\":" + (isOpen("craftingPanel") ? "true" : "false")
		+ ",\"trade\":" + (isOpen("tradeMenu") ? "true" : "false") + "}";
}))()
