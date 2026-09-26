using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// One element of the Online UI window on the game's own control (ticket
/// online-ui-art-and-controls-overhaul, S2b): the game's row prefab for the element's kind, kept alive
/// across frames and re-applied only when what it shows changed.
///
/// <para>
/// The rows come from the game's own settings screen, so the sprite, the 9-slice, the font and the control
/// itself are the game's: <c>Special/GameSettingLanguage</c> is a button with its caption on child 0, and
/// <c>GameSettingBool</c> / <c>GameSettingDropdown</c> / <c>GameSettingInt</c> / <c>GameSettingFloat</c>
/// carry their control on child 1 (the float row shows its value on child 2) — the shape
/// <c>SettingsMenu</c> itself wires up. A label has no prefab of its own, so it is built from the game's
/// own font, which the game's row hands over.
/// </para>
///
/// <para>
/// The prefab's own SIZE is carried into the layout explicitly, because the game places its rows by hand
/// (<c>SettingsMenu</c> sets <c>anchoredPosition</c> from <c>sizeDelta</c>) and a layout group takes
/// nothing from a RectTransform: without that, a row whose prefab carries no <c>LayoutElement</c> value
/// would be laid out at zero height. A model width hint overrides the width when it has one.
/// </para>
///
/// <para>
/// A view reports what the player did as intents and holds no meaning of its own: the id it reports is
/// the element's id, read at the moment of the interaction, because a view may be reused for a different
/// element once the page or the roster changes.
/// </para>
/// </summary>
internal sealed class OnlineUiControlView
{
	/// <summary>The game's own button row — the prefab its settings screen uses for a language row, and the
	/// one whose sprite and typography the window's chrome reads.</summary>
	internal const string ButtonRowPrefabPath = "Special/GameSettingLanguage";

	/// <summary>The width a placeholder takes when neither the model nor a prefab gives one: the fallback a
	/// missing prefab produces still has to be visible (and, for a button, clickable).</summary>
	internal const float PlaceholderWidth = 220f;

	/// <summary>The padding a hand-built label adds to the game's own font size for its line height.</summary>
	internal const float LabelHeightPadding = 8f;

	/// <summary>The game's own typography, handed to every label this surface builds: the font the game's
	/// row labels use and the size they are set at.</summary>
	internal readonly record struct Typography(TMP_FontAsset? Font, float Size)
	{
		/// <summary>The game's own sizes, as the IMGUI theme's four levels expressed them relative to the
		/// body text.</summary>
		internal float SizeOf(OnlineUiTextStyle style) => style switch
		{
			OnlineUiTextStyle.Title => Size + 4f,
			OnlineUiTextStyle.Section => Size - 1f,
			OnlineUiTextStyle.Muted => Size - 2f,
			_ => Size,
		};

		internal static FontStyles WeightOf(OnlineUiTextStyle style) =>
			style is OnlineUiTextStyle.Title or OnlineUiTextStyle.Section ? FontStyles.Bold : FontStyles.Normal;
	}

	/// <summary>The caption colour of a button that is the current choice (a tab, the transport switch),
	/// and of one that is not: the same accent/muted pair the IMGUI tabs used.</summary>
	private static readonly Color SelectedCaption = new(0.85f, 0.72f, 0.38f, 1f);

	private static readonly Color UnselectedCaption = new(0.92f, 0.93f, 0.94f, 1f);

	private readonly GameObject _root;
	private readonly RectTransform _rect;
	private readonly LayoutElement _layout;
	private readonly Action<OnlineUiIntent> _report;
	private readonly Typography _typography;
	private readonly Button? _button;
	private readonly Toggle? _toggle;
	private readonly TMP_Dropdown? _dropdown;
	private readonly TMP_InputField? _input;
	private readonly Slider? _slider;
	private readonly TextMeshProUGUI? _caption;
	private readonly TextMeshProUGUI? _rowLabel;
	private readonly TextMeshProUGUI? _valueText;

	private OnlineUiElementKind _kind;
	private string _id = "";
	private string _text = "\0";
	private string _value = "\0";
	private bool _selected;
	private bool _flag;
	private float _number = float.NaN;
	private float _minimum = float.NaN;
	private float _maximum = float.NaN;
	private int _index = -1;
	private int _optionCount = -1;

	private OnlineUiControlView(
		GameObject root,
		RectTransform rect,
		LayoutElement layout,
		Typography typography,
		Action<OnlineUiIntent> report,
		Button? button,
		Toggle? toggle,
		TMP_Dropdown? dropdown,
		TMP_InputField? input,
		Slider? slider,
		TextMeshProUGUI? caption,
		TextMeshProUGUI? rowLabel,
		TextMeshProUGUI? valueText)
	{
		_root = root;
		_rect = rect;
		_layout = layout;
		_typography = typography;
		_report = report;
		_button = button;
		_toggle = toggle;
		_dropdown = dropdown;
		_input = input;
		_slider = slider;
		_caption = caption;
		_rowLabel = rowLabel;
		_valueText = valueText;
	}

	internal OnlineUiElementKind Kind => _kind;

	internal string Id => _id;

	internal GameObject Root => _root;

	/// <summary>False when the game's own prefab for this element's kind could not be loaded and the
	/// placeholder was used — the window logs the miss once per kind instead of the gap passing unnoticed
	/// (the launcher's fallback reports the same way).</summary>
	internal bool UsesGamePrefab { get; private init; }

	/// <summary>
	/// Builds the view for one element under <paramref name="parent"/>. It always produces a visible, sized
	/// control: the game's own prefab when it is there, and a placeholder that still shows the element's
	/// text (and stays clickable for a button) when it is not — the surface's frame callback must never
	/// throw, and one missing prefab must not cost the whole window.
	/// </summary>
	internal static OnlineUiControlView Create(
		OnlineUiElementModel element,
		Transform parent,
		Typography typography,
		Action<OnlineUiIntent> report)
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
		// carry for that case is not wanted here. The scale is normalised once for the same reason.
		if (root.TryGetComponent<ContentSizeFitter>(out var fitter))
		{
			Object.Destroy(fitter);
		}

		// The prefab's own, hand-authored size is what a layout group cannot read off the rect, so it is
		// seeded here (a prefab that carries its own layout values keeps them); a label takes the row's
		// remaining width and the height of the game's own font line.
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

		var caption = prefab != null
			? ChildText(root.transform, 0)
			: root.GetComponent<TextMeshProUGUI>();

		var control = element.Kind is OnlineUiElementKind.Label or OnlineUiElementKind.Button
			? null
			: ChildAt(root.transform, 1);

		var view = new OnlineUiControlView(
			root,
			rect,
			layout,
			typography,
			report,
			button: element.Kind == OnlineUiElementKind.Button ? ButtonOn(root, caption) : null,
			toggle: control != null ? control.GetComponent<Toggle>() : null,
			dropdown: control != null ? control.GetComponent<TMP_Dropdown>() : null,
			input: control != null ? control.GetComponent<TMP_InputField>() : null,
			slider: control != null ? control.GetComponent<Slider>() : null,
			caption: caption,
			rowLabel: element.Kind == OnlineUiElementKind.Label ? null : caption,
			valueText: element.Kind == OnlineUiElementKind.Slider && prefab != null ? ChildText(root.transform, 2) : null)
		{
			UsesGamePrefab = prefab != null,
		};

		rect.localScale = Vector3.one;
		view.Wire();
		view.Apply(element);
		return view;
	}

	/// <summary>Applies one frame's element: only what differs from the last application is written, so a
	/// steady window does not dirty the canvas every frame.</summary>
	internal void Apply(OnlineUiElementModel element)
	{
		var kindChanged = _kind != element.Kind;
		_kind = element.Kind;
		_id = element.Id;

		if (kindChanged || _text != element.Text)
		{
			_text = element.Text;
			ApplyText(element);
		}

		if (kindChanged || _selected != element.Selected)
		{
			_selected = element.Selected;
			ApplySelected(element);
		}

		switch (element.Kind)
		{
			case OnlineUiElementKind.Toggle:
				if (kindChanged || _flag != element.Flag)
				{
					_flag = element.Flag;
					// WithoutNotify: the model is the one source of the value, and echoing it back would
					// report the element's own state as a player action.
					_toggle?.SetIsOnWithoutNotify(element.Flag);
				}

				break;
			case OnlineUiElementKind.Dropdown:
				ApplyDropdown(element, kindChanged);
				break;
			case OnlineUiElementKind.TextField:
				ApplyTextField(element, kindChanged);
				break;
			case OnlineUiElementKind.Slider:
				ApplySlider(element, kindChanged);
				break;
		}

		ApplyLayout(element);
	}

	internal void SetParent(Transform parent)
	{
		// Unity object — == (the parent may have been destroyed with a scene change)
		if (_rect.parent != parent)
		{
			_rect.SetParent(parent, worldPositionStays: false);
		}
	}

	internal void Destroy() => Object.Destroy(_root);

	private void Wire()
	{
		if (_button != null)
		{
			// The id is read when the click happens, not captured: a view outlives the element it was
			// built for (the row is reused when a page or a roster changes).
			_button.onClick.AddListener(() => _report(new OnlineUiIntent(OnlineUiIntentKind.ControlInvoked, _id)));
		}

		if (_toggle != null)
		{
			_toggle.onValueChanged.AddListener(value =>
				_report(new OnlineUiIntent(OnlineUiIntentKind.ControlToggled, _id, Flag: value)));
		}

		if (_dropdown != null)
		{
			_dropdown.onValueChanged.AddListener(index =>
				_report(new OnlineUiIntent(OnlineUiIntentKind.ControlSelected, _id, Index: index)));
		}

		if (_slider != null)
		{
			_slider.onValueChanged.AddListener(value =>
				_report(new OnlineUiIntent(OnlineUiIntentKind.ControlChanged, _id, Number: value)));
		}

		if (_input != null)
		{
			_input.onValueChanged.AddListener(text =>
				_report(new OnlineUiIntent(OnlineUiIntentKind.ControlEdited, _id, Text: text)));
		}
	}

	private void ApplyText(OnlineUiElementModel element)
	{
		switch (element.Kind)
		{
			case OnlineUiElementKind.Label:
				if (_caption != null)
				{
					_caption.text = element.Text;
					_caption.color = ToColor(element.Color);
					_caption.fontSize = _typography.SizeOf(element.Style);
					_caption.fontStyle = Typography.WeightOf(element.Style);
				}

				break;
			case OnlineUiElementKind.Button:
				if (_caption != null)
				{
					_caption.text = element.Text;
				}

				break;
			default:
				if (_rowLabel != null)
				{
					_rowLabel.text = element.Text;
				}

				break;
		}
	}

	private void ApplySelected(OnlineUiElementModel element)
	{
		if (_caption == null || _button == null)
		{
			return;
		}

		_caption.color = element.Selected ? SelectedCaption : UnselectedCaption;
	}

	private void ApplyDropdown(OnlineUiElementModel element, bool kindChanged)
	{
		if (_dropdown is not { } dropdown)
		{
			return;
		}

		var options = element.Options;
		var count = options?.Count ?? 0;
		if (kindChanged || options is null || count != _optionCount || !SameOptions(dropdown, options))
		{
			dropdown.ClearOptions();
			var data = new List<TMP_Dropdown.OptionData>(count);
			for (var index = 0; index < count; index++)
			{
				data.Add(new TMP_Dropdown.OptionData(options![index]));
			}

			if (data.Count > 0)
			{
				dropdown.AddOptions(data);
			}

			_optionCount = count;
			_index = element.OptionIndex;
			dropdown.SetValueWithoutNotify(element.OptionIndex);
			return;
		}

		if (_index != element.OptionIndex)
		{
			_index = element.OptionIndex;
			dropdown.SetValueWithoutNotify(element.OptionIndex);
		}
	}

	private void ApplyTextField(OnlineUiElementModel element, bool kindChanged)
	{
		if (_input is not { } input)
		{
			return;
		}

		if (kindChanged)
		{
			// The game's only reachable input field is the integer row's; a field that carries text rather
			// than a number says so, and the field is a plain TMP_InputField either way.
			input.contentType = TMP_InputField.ContentType.Standard;
			input.lineType = TMP_InputField.LineType.SingleLine;
			input.characterLimit = element.MaxLength;
		}

		// Never while the player is typing: the model carries the value the plugin holds, and writing it
		// into a focused field would fight the caret.
		if (!input.isFocused && _value != element.Value)
		{
			_value = element.Value;
			input.SetTextWithoutNotify(element.Value);
		}
	}

	private void ApplySlider(OnlineUiElementModel element, bool kindChanged)
	{
		if (_slider is not { } slider)
		{
			return;
		}

		if (kindChanged || !_minimum.Equals(element.Minimum) || !_maximum.Equals(element.Maximum))
		{
			_minimum = element.Minimum;
			_maximum = element.Maximum;
			slider.minValue = element.Minimum;
			slider.maxValue = element.Maximum;
		}

		if (kindChanged || !_number.Equals(element.Number))
		{
			_number = element.Number;
			slider.SetValueWithoutNotify(element.Number);
		}

		if (_valueText != null && (kindChanged || _value != element.Value))
		{
			_value = element.Value;
			_valueText.text = element.Value;
		}
	}

	/// <summary>
	/// The layout half: a width hint from the model wins, everything else stays what the view was created
	/// with (the prefab's own width, seeded in <see cref="Create"/>). A label with no hint takes whatever
	/// the row has left, which is what the IMGUI rows' flexible space did.
	/// </summary>
	private void ApplyLayout(OnlineUiElementModel element)
	{
		if (element.Width > 0f && !_layout.preferredWidth.Equals(element.Width))
		{
			_layout.preferredWidth = element.Width;
		}

		var flexible = element.Kind == OnlineUiElementKind.Label && element.Width <= 0f ? 1f : 0f;
		if (!_layout.flexibleWidth.Equals(flexible))
		{
			_layout.flexibleWidth = flexible;
		}
	}

	private static bool SameOptions(TMP_Dropdown dropdown, IReadOnlyList<string> options)
	{
		if (dropdown.options.Count != options.Count)
		{
			return false;
		}

		for (var index = 0; index < options.Count; index++)
		{
			if (!string.Equals(dropdown.options[index].text, options[index], StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// The element's own object when no game prefab can be used: a label built from the game's font (which
	/// is what a <see cref="OnlineUiElementKind.Label"/> always is), and for every other kind a placeholder
	/// that still shows the element's text — clickable when the element is a button — so a prefab the game
	/// moved or renamed degrades the look instead of leaving a blank row.
	/// </summary>
	private static GameObject CreatePlainObject(OnlineUiElementKind kind, Transform parent, Typography typography)
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
		OnlineUiElementKind.Toggle => "Special/GameSettingBool",
		OnlineUiElementKind.Dropdown => "Special/GameSettingDropdown",
		OnlineUiElementKind.TextField => "Special/GameSettingInt",
		OnlineUiElementKind.Slider => "Special/GameSettingFloat",
		_ => "",
	};

	private static Button? ButtonOn(GameObject root, TextMeshProUGUI? caption)
	{
		var button = root.GetComponent<Button>();
		if (button == null)
		{
			// A prefab the game changed may carry no button of its own; an invisible image keeps the row
			// clickable (uGUI hit-tests a graphic, not its alpha).
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

		// The caption must never swallow the click — unless it IS the row's graphic, which is the case
		// only for the plain fallback.
		if (caption != null && caption != button.targetGraphic)
		{
			caption.raycastTarget = false;
		}

		return button;
	}

	private static Transform? ChildAt(Transform root, int index) =>
		root.childCount > index ? root.GetChild(index) : null;

	private static TextMeshProUGUI? ChildText(Transform root, int index)
	{
		var child = ChildAt(root, index);
		return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
	}

	private static Color ToColor(OnlineUiNativeRgba? color) =>
		color is { } value ? new Color(value.R, value.G, value.B, value.A) : UnselectedCaption;
}
