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
/// own font, which the game's row hands over; a colour block is that same button row with the chosen
/// colour laid over the graphic the row already shows.
/// </para>
///
/// <para>
/// Which prefab draws which kind, how it is instantiated so a layout group can size it, and what stands in
/// when the game ships none all belong to <see cref="OnlineUiControlFactory"/>; this class is about the
/// element that then lives on that object.
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

	/// <summary>The image a colour block tints; only a <see cref="OnlineUiElementKind.ColorSwatch"/> has one.</summary>
	private readonly Image? _swatch;

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
	private OnlineUiNativeRgba? _color;

	private OnlineUiControlView(
		GameObject root,
		RectTransform rect,
		LayoutElement layout,
		Typography typography,
		Action<OnlineUiIntent> report,
		Button? button,
		Image? swatch,
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
		_swatch = swatch;
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

	/// <summary>True when the game ships a row prefab for this element's kind and it could not be loaded,
	/// so the placeholder stands in for it — the window logs that once per kind instead of the gap passing
	/// unnoticed (the launcher's fallback reports the same way). A label has no prefab of its own by design
	/// and is never a miss.</summary>
	internal bool MissedGamePrefab { get; private init; }

	/// <summary>
	/// Builds the view for one element under <paramref name="parent"/> on the object
	/// <see cref="OnlineUiControlFactory"/> produces for its kind, and finds the parts this view drives:
	/// the caption, the control the kind carries, the button a click lands on, and the graphic a colour
	/// block tints.
	/// </summary>
	internal static OnlineUiControlView Create(
		OnlineUiElementModel element,
		Transform parent,
		Typography typography,
		Action<OnlineUiIntent> report)
	{
		var built = OnlineUiControlFactory.Build(element, parent, typography);
		var root = built.Root;
		var rect = (RectTransform)root.transform;
		var caption = OnlineUiControlFactory.CaptionOn(built);

		// A button and a colour block ARE the row; the other kinds are a control the row carries on child 1.
		var control = element.Kind is OnlineUiElementKind.Label or OnlineUiElementKind.Button or OnlineUiElementKind.ColorSwatch
			? null
			: OnlineUiControlFactory.ChildAt(root.transform, 1);
		var button = element.Kind is OnlineUiElementKind.Button or OnlineUiElementKind.ColorSwatch
			? OnlineUiControlFactory.ButtonOn(root, caption)
			: null;

		var view = new OnlineUiControlView(
			root,
			rect,
			built.Layout,
			typography,
			report,
			button: button,
			swatch: element.Kind == OnlineUiElementKind.ColorSwatch ? OnlineUiControlFactory.SwatchImageOn(root, button) : null,
			toggle: control != null ? control.GetComponent<Toggle>() : null,
			dropdown: control != null ? control.GetComponent<TMP_Dropdown>() : null,
			input: control != null ? control.GetComponent<TMP_InputField>() : null,
			slider: control != null ? control.GetComponent<Slider>() : null,
			caption: caption,
			rowLabel: element.Kind == OnlineUiElementKind.Label ? null : caption,
			valueText: element.Kind == OnlineUiElementKind.Slider && built.UsedPrefab ? OnlineUiControlFactory.ChildText(root.transform, 2) : null)
		{
			MissedGamePrefab = built.MissedPrefab,
		};

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
			case OnlineUiElementKind.ColorSwatch:
				if (kindChanged || _color != element.Color)
				{
					_color = element.Color;
					ApplySwatchColor();
				}

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
			// built for (the row is reused when a page or a roster changes). An element with NO id is not
			// a control at all — the colour picker's preview block — so its click reports nothing.
			_button.onClick.AddListener(() =>
			{
				if (_id.Length > 0)
				{
					_report(new OnlineUiIntent(OnlineUiIntentKind.ControlInvoked, _id));
				}
			});
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
	/// The colour block's fill. The game's own control keeps its sprite, its 9-slice and its size, so the
	/// block reads as one of the game's rows; the colour laid over it is the one thing the surface chooses
	/// for the element, and it is written only when it changed.
	/// </summary>
	private void ApplySwatchColor()
	{
		if (_swatch != null)
		{
			_swatch.color = ToColor(_color);
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

	private static Color ToColor(OnlineUiNativeRgba? color) =>
		color is { } value ? new Color(value.R, value.G, value.B, value.A) : UnselectedCaption;
}
