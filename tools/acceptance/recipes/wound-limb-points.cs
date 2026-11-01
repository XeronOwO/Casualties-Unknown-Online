// recipe: wound-limb-points
// args: none
// returns: ok, count, limbs (limb:screenX:screenY|...), error, detail
//
// Where the game's own wound view puts each limb target, in GUI coordinates (origin top-left, the way a frame reads), so a batch can move the staged
// pointer onto the limb it means to treat instead of guessing at the diagram.
//
// Why this exists: `WoundViewLimb` selects a limb on POINTER ENTER, not on click (reversing/Assembly-CSharp/Assembly-CSharp/WoundViewLimb.cs:
// `public class WoundViewLimb : MonoBehaviour, IPointerEnterHandler` with `this.woundview.limbLookingAt = this.limb;` inside `OnPointerEnter`), and
// `WoundView.Update` copies `body.limbs[limbLookingAt]` into `PlayerCamera.main.selectedLimb` every frame — the field the item's own limb action
// reads. So the honest local-treatment entry is: move the pointer onto the target's rect, then use the item. The rects are laid out around the body
// diagram and do not line up with where the body is DRAWN (measured 2026-11-01: a point over the drawn arm selected the abdomen), which is why this
// recipe reads the game's own anchors rather than a screenshot.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var found = UnityEngine.Object.FindObjectsOfType<WoundViewLimb>();
	var text = "";
	var count = 0;
	for (var i = 0; i < found.Length; i++) {
		var widget = found[i];
		if (widget == null || widget.woundview == null) { continue; }
		if (!widget.gameObject.activeInHierarchy) { continue; }
		var rect = widget.transform as UnityEngine.RectTransform;
		if (rect == null) { continue; }
		var camera = (UnityEngine.Camera)null;
		var canvas = widget.GetComponentInParent<UnityEngine.Canvas>();
		if (canvas != null && canvas.renderMode != UnityEngine.RenderMode.ScreenSpaceOverlay) { camera = canvas.worldCamera; }
		var corner = rect.TransformPoint(new UnityEngine.Vector3(rect.rect.center.x, rect.rect.center.y, 0f));
		var screen = UnityEngine.RectTransformUtility.WorldToScreenPoint(camera, corner);
		var guiY = UnityEngine.Screen.height - screen.y;
		if (text.Length > 0) { text += "|"; }
		text += widget.limb.ToString(inv) + ":" + screen.x.ToString("0", inv) + ":" + guiY.ToString("0", inv);
		count++;
	}

	return "{\"ok\":true,\"count\":" + count.ToString(inv) + ",\"limbs\":\"" + text + "\"}";
}))()
