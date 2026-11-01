// recipe: ui-click
// args: x=n y=n limb=n
// returns: ok, x, y, hitCount, target, handler, invoked, error, detail
//
// Clicks the GAME's own UI at a GUI point (origin top-left, the way a frame reads) by dispatching the EventSystem's click the way a real press does:
// `EventSystem.RaycastAll` runs at that point with a real PointerEventData, and the first hit that carries a pointer-click handler gets it through
// `ExecuteEvents.Execute`. Nothing is fabricated and no OS input is used: the cast list is the EventSystem's own answer, and the handler is the game's
// own component. This is the in-process analogue of a left click on a button, which is what a settings row, a tab or a key-bind row needs.
//
// Why it exists: the wound view selects a limb by clicking the drawn limb, and the settings menu starts a key-bind capture on a row's click. Neither has
// a CUO control id to click, so a point dispatch is the honest entry — and, unlike a staged mouse press, it produces no `Input.GetKeyDown(Mouse0)` edge,
// so a capture started this way keeps waiting for a keyboard key (which is exactly the state a key-bind row's row needs).
//
// ONE LINE ON PURPOSE: the client's evaluator is a Mono.CSharp REPL that takes one top-level expression, and a body that opens a block on one line and
// closes it on another is refused as (1,1): InteractiveHost (measured, ticket new-recipe-files-are-rejected-by-the-evaluator).
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var limbIndex = (int)({{n:limb}});
	var es = UnityEngine.EventSystems.EventSystem.current;
	if (es == null) { return "{\"ok\":false,\"error\":\"no-event-system\"}"; }
	var data = new UnityEngine.EventSystems.PointerEventData(es);
	data.position = new UnityEngine.Vector2(x, y);
	data.pressPosition = data.position;
	data.button = UnityEngine.EventSystems.PointerEventData.InputButton.Left;
	data.clickCount = 1;

	// A named limb target, when the caller passes one: the wound view's fifteen targets are CHILD widgets under an image the raycast returns first, so a
	// point dispatch lands on the parent and nothing happens (measured 2026-11-01: `target=Limbs enter=False`). Addressing the widget by the limb index the
	// game itself stores on it (`WoundViewLimb.limb`) dispatches the same `OnPointerEnter` the pointer would, on the object that actually implements it.
	if (limbIndex >= 0) {
		var widgets = UnityEngine.Object.FindObjectsOfType<WoundViewLimb>();
		var chosen = (WoundViewLimb)null;
		for (var i = 0; i < widgets.Length; i++) {
			if (widgets[i] != null && widgets[i].limb == limbIndex && widgets[i].gameObject.activeInHierarchy) { chosen = widgets[i]; break; }
		}
		if (chosen == null) { return "{\"ok\":false,\"error\":\"no-such-limb-widget\",\"limb\":" + limbIndex.ToString(inv) + "}"; }
		UnityEngine.EventSystems.ExecuteEvents.Execute(chosen.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
		return "{\"ok\":true,\"limb\":" + limbIndex.ToString(inv) + ",\"target\":\"" + chosen.gameObject.name + "\",\"enter\":true,\"invoked\":true}";
	}

	var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
	es.RaycastAll(data, results);
	if (results.Count == 0) { return "{\"ok\":false,\"error\":\"no-hit\",\"x\":" + x.ToString(inv) + ",\"y\":" + y.ToString(inv) + "}"; }

	var target = results[0].gameObject;
	// A real pointer produces ENTER (hover), then DOWN, UP and CLICK; the game's own widgets answer different ones — the wound view's limb diagram is an
	// `IPointerEnterHandler` (reversing/Assembly-CSharp/Assembly-CSharp/WoundViewLimb.cs: `OnPointerEnter` sets `limbLookingAt`), while a settings row's
	// button is a click target. Each handler is dispatched only when the object carries one, and the answer names every dispatch so a reading says which
	// one the widget answered.
	var enter = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerEnterHandler>(target);
	var down = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerDownHandler>(target);
	var up = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerUpHandler>(target);
	var click = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(target);
	var invoked = enter != null || down != null || up != null || click != null;
	if (enter != null) { UnityEngine.EventSystems.ExecuteEvents.Execute(enter, data, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler); }
	if (down != null) { UnityEngine.EventSystems.ExecuteEvents.Execute(down, data, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler); }
	if (up != null) { UnityEngine.EventSystems.ExecuteEvents.Execute(up, data, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler); }
	if (click != null) { UnityEngine.EventSystems.ExecuteEvents.Execute(click, data, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler); }

	return "{\"ok\":true,\"x\":" + x.ToString(inv) + ",\"y\":" + y.ToString(inv)
		+ ",\"hitCount\":" + results.Count.ToString(inv)
		+ ",\"target\":\"" + target.name + "\""
		+ ",\"enter\":" + (enter != null ? "true" : "false")
		+ ",\"down\":" + (down != null ? "true" : "false")
		+ ",\"up\":" + (up != null ? "true" : "false")
		+ ",\"click\":" + (click != null ? "true" : "false")
		+ ",\"invoked\":" + (invoked ? "true" : "false") + "}";
}))()
