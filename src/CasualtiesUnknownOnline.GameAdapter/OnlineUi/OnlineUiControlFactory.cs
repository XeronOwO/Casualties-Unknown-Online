using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The game's own control for one element of the Online UI window (ticket online-ui-art-and-controls-overhaul,
/// S2b, extended by S3's colour block): which of the game's row prefabs draws which
/// <see cref="OnlineUiElementKind"/>, how that prefab is instantiated so the layout group that owns it can
/// size it, how its graphic is made clickable and tintable, and what stands in when the game ships no
/// prefab for the kind or the one it ships cannot be loaded.
///
/// <para>
/// It is a type of its own because this is the adapter's knowledge of the GAME's prefabs — the part that
/// changes when a kind is added or the game moves a row — while <see cref="OnlineUiControlView"/> is about
/// driving one element once it has an object. Splitting them keeps the view a view, and keeps a missing or
/// renamed prefab a fact this type reports instead of a hole the view falls into.
/// </para>
/// </summary>
internal static class OnlineUiControlFactory
{
	/// <summary>The game's own button row — the prefab its settings screen uses for a language row, and the
	/// one whose sprite and typography the window's chrome reads.</summary>
	internal const string ButtonRowPrefabPath = "Special/GameSettingLanguage";

	/// <summary>The width a placeholder takes when neither the model nor a prefab gives one: the fallback a
	/// missing prefab produces still has to be visible (and, for a button, clickable).</summary>
	internal const float PlaceholderWidth = 220f;

	/// <summary>The padding a hand-built label adds to the game's own font size for its line height.</summary>
	internal const float LabelHeightPadding = 8f;

	/// <summary>
	/// The object one element is drawn on, ready for a layout group: the game's own row prefab for the
	/// kind when the game ships one, the placeholder otherwise. <paramref name="UsedPrefab"/> says which of
	/// the two it is, and <paramref name="MissedPrefab"/> is narrower than its negation — a label has no
	/// prefab of its own BY DESIGN and is not a miss, while a button whose row could not be loaded is one
	/// the window reports.
	/// </summary>
	internal readonly record struct Built(GameObject Root, LayoutElement Layout, bool UsedPrefab, bool MissedPrefab);

	/// <summary>
	/// Builds the object for one element under <paramref name="parent"/>. It always produces a visible,
	/// sized control: the game's own prefab when it is there, and a placeholder that still shows the
	/// element's text (and stays clickable for a button) when it is not — the surface's frame callback must
	/// never throw, and one missing prefab must not cost the whole window.
	///
	/// <para>
	/// The prefab's own SIZE is carried into the layout explicitly, because the game places its rows by
	/// hand (<c>SettingsMenu</c> sets <c>anchoredPosition</c> from <c>sizeDelta</c>) and a layout group
	/// takes nothing from a RectTransform: without that, a row whose prefab carries no
	/// <c>LayoutElement</c> value would be laid out at zero height. A model width hint overrides the width
	/// later, in the view.
	/// </para>
	/// </summary>
	internal static Built Build(OnlineUiElementModel element, Transform parent, OnlineUiControlView.Typography typography)
	{
		var prefabPath = element.Kind == OnlineUiElementKind.Label ? null : PrefabPathOf(element.Kind);
		var prefab = prefabPath is { Length: > 0 } ? Resources.Load<GameObject>(prefabPath) : null;
		var root = prefab != null
			? Object.Instantiate(prefab, parent)
			: CreatePlainObject(element.Kind, parent, typography);

		if (root.transform is not RectTransform rect)
		{
			Object.Destroy(root);
			root = CreatePlainObject(element.Kind, parent, typography);
			rect = (RectTransform)root.transform;
		}

		// A ContentSizeFitter on the instantiated prefab would fight the layout group that owns this
		// element's size; the row prefabs are placed by hand in the game's own screen, so whatever they
		// carry for that case is not wanted here.
		if (root.TryGetComponent<ContentSizeFitter>(out var fitter))
		{
			Object.Destroy(fitter);
		}

		var authored = prefab != null ? rect.sizeDelta : Vector2.zero;
		var authoredWidth = authored.x > 0f
			? authored.x
			: element.Kind == OnlineUiElementKind.Label ? 0f : PlaceholderWidth;
		var authoredHeight = authored.y > 0f ? authored.y : typography.Size + LabelHeightPadding;
		var layout = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
		if (layout.preferredHeight <= 0f)
		{
			layout.preferredHeight = authoredHeight;
		}

		if (layout.preferredWidth <= 0f && authoredWidth > 0f)
		{
			layout.preferredWidth = authoredWidth;
		}

		// The scale is normalised once for the same reason the fitter goes: the row is laid out by CUO's
		// own groups now, not by the game's hand placement.
		rect.localScale = Vector3.one;
		return new Built(
			root,
			layout,
			UsedPrefab: prefab != null,
			MissedPrefab: prefab == null && prefabPath is { Length: > 0 });
	}

	/// <summary>The row's caption: the game's own row carries it on child 0, while the placeholder IS a
	/// text object.</summary>
	internal static TextMeshProUGUI? CaptionOn(Built built) =>
		built.UsedPrefab ? ChildText(built.Root.transform, 0) : built.Root.GetComponent<TextMeshProUGUI>();

	/// <summary>
	/// Reads what the game's own row prefab carries — the font and size of its label, and the sprite, type
	/// and pixels-per-unit multiplier of its background — so a surface's chrome (the window's frame, a
	/// panel's) and every label on it are the game's rather than a guess. The template instance is
	/// destroyed in the same call: it exists to be read.
	/// </summary>
	internal static void ReadRowTemplate(
		Transform parent,
		out OnlineUiControlView.Typography typography,
		out Sprite? sprite,
		out Image.Type imageType,
		out float pixelsPerUnit)
	{
		const float fallbackSize = 14f;
		sprite = null;
		imageType = Image.Type.Simple;
		pixelsPerUnit = 1f;

		var prefab = Resources.Load<GameObject>(ButtonRowPrefabPath);
		if (prefab is null)
		{
			typography = new OnlineUiControlView.Typography(TMP_Settings.defaultFontAsset, fallbackSize);
			return;
		}

		var probe = Object.Instantiate(prefab, parent);
		probe.SetActive(false);
		var label = probe.transform.childCount > 0 ? probe.transform.GetChild(0).GetComponent<TextMeshProUGUI>() : null;
		var image = probe.GetComponent<Image>();
		var font = label != null && label.font != null ? label.font : TMP_Settings.defaultFontAsset;
		var size = label != null && label.fontSize > 0f ? label.fontSize : fallbackSize;
		typography = new OnlineUiControlView.Typography(font, size);
		if (image != null)
		{
			sprite = image.sprite;
			imageType = image.type;
			pixelsPerUnit = image.pixelsPerUnitMultiplier;
		}

		Object.Destroy(probe);
	}

	/// <summary>The child at <paramref name="index"/>, or null when the row carries fewer.</summary>
	internal static Transform? ChildAt(Transform root, int index) =>
		root.childCount > index ? root.GetChild(index) : null;

	/// <summary>The text on the child at <paramref name="index"/>, or null when it carries none.</summary>
	internal static TextMeshProUGUI? ChildText(Transform root, int index)
	{
		var child = ChildAt(root, index);
		return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
	}

	/// <summary>
	/// The element's own object when no game prefab can be used: a label built from the game's font (which
	/// is what a <see cref="OnlineUiElementKind.Label"/> always is), and for every other kind a placeholder
	/// that still shows the element's text — clickable when the element is a button — so a prefab the game
	/// moved or renamed degrades the look instead of leaving a blank row.
	/// </summary>
	private static GameObject CreatePlainObject(OnlineUiElementKind kind, Transform parent, OnlineUiControlView.Typography typography)
	{
		var name = kind == OnlineUiElementKind.Label ? "CUO Online UI Label" : $"CUO Online UI {kind} (no game prefab)";
		var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		root.transform.SetParent(parent, worldPositionStays: false);
		var text = root.GetComponent<TextMeshProUGUI>();
		text.alignment = TextAlignmentOptions.Left;
		text.fontSize = typography.Size;
		if (typography.Font != null)
		{
			text.font = typography.Font;
		}

		return root;
	}

	private static string PrefabPathOf(OnlineUiElementKind kind) => kind switch
	{
		OnlineUiElementKind.Button => ButtonRowPrefabPath,
		OnlineUiElementKind.ColorSwatch => ButtonRowPrefabPath,
		OnlineUiElementKind.Toggle => "Special/GameSettingBool",
		OnlineUiElementKind.Dropdown => "Special/GameSettingDropdown",
		OnlineUiElementKind.TextField => "Special/GameSettingInt",
		OnlineUiElementKind.Slider => "Special/GameSettingFloat",
		_ => "",
	};

	/// <summary>
	/// The image a colour block tints: the graphic the row's button actually shows (a uGUI button fades
	/// <c>targetGraphic</c> for its own states, so that is the surface the player sees), the row's own
	/// image when the button carries none, and a plain image when the fallback object has none either —
	/// a prefab the game moved must still show the colour instead of a blank row.
	/// </summary>
	internal static Image? SwatchImageOn(GameObject root, Button? button)
	{
		if (button != null && button.targetGraphic is Image target)
		{
			return target;
		}

		var image = root.GetComponent<Image>();
		return image != null ? image : root.AddComponent<Image>();
	}

	/// <summary>
	/// The button a click lands on. A prefab the game changed may carry no button of its own, so one is
	/// added over an invisible image (uGUI hit-tests a graphic, not its alpha), and the caption never
	/// swallows the click unless it IS the row's graphic.
	/// </summary>
	internal static Button? ButtonOn(GameObject root, TextMeshProUGUI? caption)
	{
		var button = root.GetComponent<Button>();
		if (button == null)
		{
			var graphic = root.GetComponent<Graphic>();
			if (graphic == null)
			{
				var image = root.AddComponent<Image>();
				image.color = new Color(0f, 0f, 0f, 0f);
				graphic = image;
			}

			button = root.AddComponent<Button>();
			button.targetGraphic = graphic;
		}

		if (caption != null && caption != button.targetGraphic)
		{
			caption.raycastTarget = false;
		}

		return button;
	}
}
