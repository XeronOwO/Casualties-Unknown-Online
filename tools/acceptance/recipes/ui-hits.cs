// recipe: ui-hits
// args: none
// returns: ok, x, y, count, hits, error, detail
//
// Runs the game's OWN raycast (`EventSystem.current.RaycastAll`) at the pointer position the game itself reads
// (`Input.mousePosition`) and lists what it hits. The drag chain starts from exactly this list (`UIUtil.GetEventSystemRaycastResults`
// feeds `PlayerCamera.TryPickupFromUI`), so this separates "the pointer is in the wrong place" from "the widget is not a raycast
// target at all". One line: the evaluator takes a single top-level expression.
((System.Func<string>)(() => { var inv = System.Globalization.CultureInfo.InvariantCulture; var system = UnityEngine.EventSystems.EventSystem.current; if (system == null) { return "{\"ok\":false,\"error\":\"no-event-system\"}"; } var mouse = UnityEngine.Input.mousePosition; var data = new UnityEngine.EventSystems.PointerEventData(system); data.position = new UnityEngine.Vector2(mouse.x, mouse.y); var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>(); system.RaycastAll(data, results); var parts = new System.Collections.Generic.List<string>(); for (var i = 0; i < results.Count && i < 20; i++) { parts.Add("{\"name\":\"" + results[i].gameObject.name + "\",\"layer\":\"" + UnityEngine.LayerMask.LayerToName(results[i].gameObject.layer) + "\"}"); } return "{\"ok\":true,\"x\":" + mouse.x.ToString("0", inv) + ",\"y\":" + mouse.y.ToString("0", inv) + ",\"count\":" + results.Count.ToString(inv) + ",\"hits\":[" + string.Join(",", parts) + "]}"; }))()