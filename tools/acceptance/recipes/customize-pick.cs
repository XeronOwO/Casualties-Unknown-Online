// recipe: customize-pick
// args: swatch=n label=s
// serves: 1.0-rebuild-each-acceptance-row-from-the-game (row 9, item-customization)
// returns: ok, label, swatch, active, error
//
// Picks a colour and types a label on the OPEN customizer through the game's own handlers: the swatch button's
// `onClick` delegate writes `customizedItem.colorSwatch` (`PlayerCamera.cs:835-845`) and the label field's
// `onValueChanged` writes `customizedItem.label` (`PlayerCamera.cs:859-866`). The customizer's parts are private, so
// they are reached the way the game reaches them, by reflection on the camera object; nothing here writes the item's
// fields directly.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var swatch = {{n:swatch}};
	var label = {{s:label}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var cameraType = camera.GetType();
	var itemField = cameraType.GetField("customizedItem", Flags);
	var item = itemField == null ? null : itemField.GetValue(camera);
	if (item == null) { return "{\"ok\":false,\"error\":\"no-customizer-open\"}"; }
	var swatchesField = cameraType.GetField("colorSwatches", Flags);
	var swatches = swatchesField == null ? null : swatchesField.GetValue(camera) as System.Collections.IList;
	if (swatches == null) { return "{\"ok\":false,\"error\":\"no-swatches\"}"; }
	if (swatch < 0 || swatch >= swatches.Count) { return "{\"ok\":false,\"error\":\"no-such-swatch\"}"; }
	var button = swatches[swatch] as UnityEngine.UI.Button;
	if (button == null) { return "{\"ok\":false,\"error\":\"no-swatch-button\"}"; }
	button.onClick.Invoke();
	var labelField = cameraType.GetField("customItemLabel", Flags);
	var input = labelField == null ? null : labelField.GetValue(camera) as TMPro.TMP_InputField;
	if (input == null) { return "{\"ok\":false,\"error\":\"no-label-field\"}"; }
	input.text = label;
	input.onValueChanged.Invoke(label);
	var itemType = item.GetType();
	var labelValue = itemType.GetField("label", Flags);
	var swatchValue = itemType.GetField("colorSwatch", Flags);
	var screen = cameraType.GetField("itemCustomizer", Flags);
	var screenObject = screen == null ? null : screen.GetValue(camera) as UnityEngine.GameObject;
	var active = screenObject != null && screenObject.activeSelf;
	return "{\"ok\":true,\"label\":\"" + (labelValue == null ? "" : (labelValue.GetValue(item) as string ?? "")) + "\",\"swatch\":" + (swatchValue == null ? -1 : System.Convert.ToInt32(swatchValue.GetValue(item), inv)) + ",\"active\":" + (active ? "true" : "false") + "}";
}))()
