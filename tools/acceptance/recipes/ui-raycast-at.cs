// recipe: ui-raycast-at
// args: x=n y=n
// serves: context-menu-does-not-block-the-pointer (the cause), and any row about whether the game's pointer-over-UI gate sees a CUO surface
// returns: ok, uiLayer, hitCount, isUiHit, first, firstLayer, error
//
// THE QUESTION THE GAME ASKS, ASKED BACK. UIUtil.IsPointerOverUIElement (reversing/Assembly-CSharp/Assembly-CSharp/UIUtil.cs) raycasts the
// EventSystem at the pointer and answers true for the first hit whose GameObject is on the "UI" layer; PlayerCamera.HandleAttacks
// (PlayerCamera.cs:1826) reads it before it mines or uses an item. This asks the same question at a chosen GUI point (origin top-left, the way
// a frame reads), so a click that "went through" a CUO panel can be explained without a mouse.
//
// ONE LINE ON PURPOSE: the client's evaluator is a Mono.CSharp REPL that takes one top-level expression, and a body that opens a block on one
// line and closes it on another is refused as (1,1): InteractiveHost (measured, ticket new-recipe-files-are-rejected-by-the-evaluator).
((System.Func<string>)(() => { var x = (float)({{n:x}}); var y = (float)({{n:y}}); var system = UnityEngine.EventSystems.EventSystem.current; if (system == null) { return "{\"ok\":false,\"error\":\"no-eventsystem\"}"; } var ui = UnityEngine.LayerMask.NameToLayer("UI"); var data = new UnityEngine.EventSystems.PointerEventData(system); data.position = new UnityEngine.Vector2(x, (float)UnityEngine.Screen.height - y); var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>(); system.RaycastAll(data, hits); var first = hits.Count > 0 ? hits[0].gameObject.name : "none"; var layer0 = hits.Count > 0 ? hits[0].gameObject.layer : -1; var isUi = hits.Count > 0 && layer0 == ui; return "{\"ok\":true,\"uiLayer\":" + ui + ",\"hitCount\":" + hits.Count + ",\"isUiHit\":" + (isUi ? "true" : "false") + ",\"first\":\"" + first + "\",\"firstLayer\":" + layer0 + "}"; }))()
