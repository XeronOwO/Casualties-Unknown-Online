// recipe: panel-controls
// args: none
// serves: remote-medical-panel-hide-local-only-actions (rows 1, 2 and 5: what the panel presents while a remote focus is open)
// returns: ok, viewOpen, remoteFocus, napFound, napActive, sleepImageActive, workoutFound, workoutActive, handSwapFound, handSwapActive, error, detail
//
// Reads the three local-only surfaces the ticket hides - the nap control (`WoundView.napbutton`) with its sleep-state icon, the
// workout list (`WoundView.workoutList`) and the HUD main/off-hand switch (`PlayerCamera.handSwapImage`) - as the game itself
// holds them. The ticket's rows are about whether they are PRESENTED, so the reading is each surface's own `activeInHierarchy`
// plus whether a remote focus is open, not whether a button is merely non-interactable.
((System.Func<string>)(() => {
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var view = WoundView.view;
	var camera = PlayerCamera.main;
	if (view == null || camera == null) { return "{\"ok\":false,\"error\":\"no-view-or-camera\"}"; }
	var napField = typeof(WoundView).GetField("napbutton", flags);
	var sleepField = typeof(WoundView).GetField("sleepImage", flags);
	var workoutField = typeof(WoundView).GetField("workoutList", flags);
	var handField = typeof(PlayerCamera).GetField("handSwapImage", flags);
	var napValue = napField == null ? null : napField.GetValue(view);
	var sleepValue = sleepField == null ? null : sleepField.GetValue(view);
	var nap = napValue is UnityEngine.Component napComponent ? napComponent.gameObject : napValue as UnityEngine.GameObject;
	var sleep = sleepValue is UnityEngine.Component sleepComponent ? sleepComponent.gameObject : sleepValue as UnityEngine.GameObject;
	var workout = workoutField == null ? null : workoutField.GetValue(view) as UnityEngine.GameObject;
	var handValue = handField == null ? null : handField.GetValue(camera);
	var hand = handValue is UnityEngine.Component handComponent ? handComponent.gameObject : handValue as UnityEngine.GameObject;
	var focusType = (System.Type)null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		if (focusType == null) { try { focusType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.RemoteMedicalLocalControls", false); } catch { focusType = null; } }
	}
	var remoteFocus = false;
	var displayBody = "none";
	var viewType = (System.Type)null;
	for (var i = 0; i < assemblies.Length; i++) {
		if (viewType == null) { try { viewType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalView", false); } catch { viewType = null; } }
	}
	if (viewType != null) {
		var displayProp = viewType.GetProperty("DisplayBody", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		if (displayProp != null) {
			var displayValue = displayProp.GetValue(null) as UnityEngine.Object;
			remoteFocus = displayValue != null;
			if (displayValue != null) { displayBody = displayValue.name; }
		}
	}
	return "{\"ok\":true"
		+ ",\"viewOpen\":" + (view.gameObject.activeInHierarchy ? "true" : "false")
		+ ",\"remoteFocus\":" + (remoteFocus ? "true" : "false")
		+ ",\"displayBody\":\"" + displayBody + "\""
		+ ",\"napFound\":" + (nap != null ? "true" : "false")
		+ ",\"napActive\":" + (nap != null && nap.activeInHierarchy ? "true" : "false")
		+ ",\"sleepImageActive\":" + (sleep != null && sleep.gameObject.activeInHierarchy ? "true" : "false")
		+ ",\"workoutFound\":" + (workout != null ? "true" : "false")
		+ ",\"workoutActive\":" + (workout != null && workout.activeInHierarchy ? "true" : "false")
		+ ",\"handSwapFound\":" + (hand != null ? "true" : "false")
		+ ",\"handSwapActive\":" + (hand != null && hand.gameObject.activeInHierarchy ? "true" : "false")
		// The HUD swap control is hidden by disabling its GRAPHIC, not its object (the object also carries the hand slots), so
		// the object's own active state says nothing about whether the player can see it. This field is the one that answers.
		+ ",\"handSwapGraphicEnabled\":" + (handValue is UnityEngine.UI.Image swapImage ? (swapImage.enabled ? "true" : "false") : "unknown") + "}";
}))()
