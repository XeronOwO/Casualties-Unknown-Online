// recipe: pointer-space
// args: none
// returns: ok, screenWidth, screenHeight, cameraWidth, cameraHeight, inputX, inputY, scaleX, scaleY, worldX, worldY
//
// Reports the pixel spaces a row has to keep apart: the window the game draws into (`Screen.width/height`), the camera's
// own pixel rect (`Camera.pixelWidth/pixelHeight`), and the pointer the game reads (`Input.mousePosition`) together with
// the world point that pointer maps to. Measured 2026-11-22: the row staged (662, 384), the game read (1728, -159), and
// the game's camera put the aimed block at (-7577.98, -7608.02) — three different spaces, and every aim derived from the
// first one landed beside the block.
//
// `scaleX/scaleY` are what turns one space into the other; a row that stages a position multiplies by them first, or it is
// pointing somewhere the game never looks.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => { var inv = System.Globalization.CultureInfo.InvariantCulture; var cam = UnityEngine.Camera.main; var input = UnityEngine.Input.mousePosition; var camW = cam == null ? 0 : cam.pixelWidth; var camH = cam == null ? 0 : cam.pixelHeight; var world = cam == null ? UnityEngine.Vector3.zero : cam.ScreenToWorldPoint(input); return "{\"ok\":true,\"screenWidth\":" + UnityEngine.Screen.width.ToString(inv) + ",\"screenHeight\":" + UnityEngine.Screen.height.ToString(inv) + ",\"cameraWidth\":" + camW.ToString(inv) + ",\"cameraHeight\":" + camH.ToString(inv) + ",\"inputX\":" + input.x.ToString("0.#", inv) + ",\"inputY\":" + input.y.ToString("0.#", inv) + ",\"scaleX\":" + (UnityEngine.Screen.width / (camW == 0 ? 1f : camW)).ToString("0.####", inv) + ",\"scaleY\":" + (UnityEngine.Screen.height / (camH == 0 ? 1f : camH)).ToString("0.####", inv) + ",\"worldX\":" + world.x.ToString("0.###", inv) + ",\"worldY\":" + world.y.ToString("0.###", inv) + "}"; }))()
