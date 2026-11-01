// recipe: inv-slots
// args: none
// returns: ok, count, slots, error, detail
//
// Lists the game's own inventory slot widgets (`InvButton`, and `ItemLabel` where a slot carries one) with the SCREEN point
// their rect lands on and the item id they hold. It exists because the cross-player drag begins in `PlayerCamera`'s own UI
// raycast for exactly these components (`dragItem = invButton.GetItem()`), so a staged pointer press only starts a drag when
// it lands on one of them — and guessing the wheel's geometry wasted four attempts.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => { var inv = System.Globalization.CultureInfo.InvariantCulture; var camera = UnityEngine.Camera.main; if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; } var buttons = UnityEngine.Object.FindObjectsOfType(typeof(InvButton), true); var parts = new System.Collections.Generic.List<string>(); var count = 0; for (var i = 0; i < buttons.Length; i++) { var button = buttons[i] as InvButton; if (button == null) { continue; } var rect = button.transform as UnityEngine.RectTransform; if (rect == null) { continue; } var item = button.GetItem(); var itemName = item == null ? "" : item.id; var corners = new UnityEngine.Vector3[4]; rect.GetWorldCorners(corners); var centre = (corners[0] + corners[2]) * 0.5f; var screen = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, centre); parts.Add("{\"name\":\"" + button.gameObject.name + "\",\"item\":\"" + itemName + "\",\"worldX\":" + centre.x.ToString("0.##", inv) + ",\"worldY\":" + centre.y.ToString("0.##", inv) + ",\"screenX\":" + screen.x.ToString("0", inv) + ",\"screenY\":" + screen.y.ToString("0", inv) + ",\"active\":" + (button.gameObject.activeInHierarchy ? "true" : "false") + "}"); count++; if (count >= 40) { break; } } return "{\"ok\":true,\"count\":" + count.ToString(inv) + ",\"slots\":[" + string.Join(",", parts) + "]}"; }))()