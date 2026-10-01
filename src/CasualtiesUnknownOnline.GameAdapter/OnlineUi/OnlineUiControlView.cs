using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// One element of the Online UI window on the game's own control (tickets online-ui-art-and-controls-overhaul
/// S2b, online-ui-layout-and-input-detail-pass S2): the game's row prefab for the element's kind, kept alive
/// across frames and re-applied only when what it shows changed.
///
/// <para>
/// The rows come from the game's own settings screen, so the sprite, the 9-slice, the font and the control
/// itself are the game's: <c>Special/GameSettingLanguage</c> is a button with its caption on child 0, and
/// <c>GameSettingBool</c> / <c>GameSettingDropdown</c> / <c>GameSettingInt</c> / <c>GameSettingFloat</c>
/// carry their control on child 1 (the float row shows its value on child 2) — the shape <c>SettingsMenu</c>
/// itself wires up. A label has no prefab of its own and is built from the game's own font; a colour block is
/// that same button row with the chosen colour laid over the graphic it already shows. Which prefab draws
/// which kind, and what stands in when the game ships none, belong to <see cref="OnlineUiControlFactory"/> —
/// how wide the control has to be is <see cref="OnlineUiControlSizing"/>'s, and where the parts INSIDE one of
/// the game's rows sit is <see cref="OnlineUiRowGeometry"/>'s.
/// </para>
///
/// <para>
/// A view reports what the player did as intents and holds no meaning of its own: the id it reports is the
/// element's id, read when the interaction happens, because a view may be reused for another element.
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

	/// <summary>The caption colour of the current choice (a tab, the transport switch) and of the others.</summary>
	private static readonly Color SelectedCaption = new(0.85f, 0.72f, 0.38f, 1f);

	private static readonly Color UnselectedCaption = new(0.92f, 0.93f, 0.94f, 1f);

	/// <summary>The game's own sprite colour for a button that is not the current choice: untinted.</summary>
	private static readonly Color UnselectedFill = Color.white;

	/// <summary>A warm wash over the game's own sprite for the current choice, so the open page and the
	/// transport the session uses are visible at a glance and not only in the caption.</summary>
	private static readonly Color SelectedFill = new(1f, 0.86f, 0.6f, 1f);

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

	/// <summary>The game's own row label (child 0), when the prefab carries one: the geometry inside the row is
	/// CUO's, and this is the half of it that takes what the control does not.</summary>
	private readonly RectTransform? _labelRect;

	/// <summary>The control the row carries on child 1, sized by CUO inside the row it belongs to.</summary>
	private readonly RectTransform? _controlRect;

	/// <summary>The graphic a selected button tints: the tab row's current page and the Home page's transport
	/// switch read as chosen, not only as a differently coloured caption.</summary>
	private readonly Image? _selectionGraphic;

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
		TextMeshProUGUI? valueText,
		RectTransform? labelRect,
		RectTransform? controlRect,
		Image? selectionGraphic)
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
		_labelRect = labelRect;
		_controlRect = controlRect;
		_selectionGraphic = selectionGraphic;
	}

	internal OnlineUiElementKind Kind => _kind;

	internal string Id => _id;

	internal GameObject Root => _root;

	/// <summary>True when the game ships a row prefab for this kind and it could not be loaded, so the
	/// placeholder stands in — the window logs that once per kind. A label has no prefab by design.</summary>
	internal bool MissedGamePrefab { get; private init; }

	/// <summary>True when the game's row left this control without a graphic that accepts the raycast and CUO
	/// had to give it one: the window reports it once per kind, because a box nobody can click is a defect.</summary>
	internal bool FixedPointerSurface { get; private init; }

	/// <summary>Builds the view for one element under <paramref name="parent"/> on the object
	/// <see cref="OnlineUiControlFactory"/> produces for its kind, and finds the parts this view drives.</summary>
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
		var carriesNoControl = element.Kind is OnlineUiElementKind.Label or OnlineUiElementKind.Button or OnlineUiElementKind.ColorSwatch;
		var control = carriesNoControl ? null : OnlineUiControlFactory.ChildAt(root.transform, 1);
		var controlRect = control as RectTransform;
		var button = element.Kind is OnlineUiElementKind.Button or OnlineUiElementKind.ColorSwatch
			? OnlineUiControlFactory.ButtonOn(root, caption)
			: null;
		var toggle = control != null ? control.GetComponent<Toggle>() : null;
		var dropdown = control != null ? control.GetComponent<TMP_Dropdown>() : null;
		var input = control != null ? control.GetComponent<TMP_InputField>() : null;
		var slider = control != null ? control.GetComponent<Slider>() : null;

		// The game places a row's children by hand for its own screen, so an instantiated row keeps those
		// rects: the control's insides are stretched onto the box CUO sizes here (which is what puts a
		// dropdown's value inside its frame and a field's caret inside its box), and a label that is not a
		// control stops swallowing clicks meant for the control beside it.
		OnlineUiRowGeometry.PrepareInternals(dropdown, input, toggle);
		if (button is null)
		{
			OnlineUiRowGeometry.MakeLabelTransparentToPointer(caption);
		}

		// A field or a dropdown is clicked on its own box; when the game's prefab leaves that box without a
		// graphic that accepts the raycast, the player has a control that shows text and answers nothing.
		var pointerFixed = false;
		if (control != null && element.Kind is OnlineUiElementKind.Dropdown or OnlineUiElementKind.TextField)
		{
			pointerFixed = OnlineUiRowGeometry.EnsurePointerSurface(control.gameObject);
		}

		var view = new OnlineUiControlView(
			root,
			rect,
			built.Layout,
			typography,
			report,
			button: button,
			swatch: element.Kind == OnlineUiElementKind.ColorSwatch ? OnlineUiControlFactory.SwatchImageOn(root, button) : null,
			toggle: toggle,
			dropdown: dropdown,
			input: input,
			slider: slider,
			caption: caption,
			rowLabel: element.Kind == OnlineUiElementKind.Label ? null : caption,
			valueText: element.Kind == OnlineUiElementKind.Slider && built.UsedPrefab ? OnlineUiControlFactory.ChildText(root.transform, 2) : null,
			labelRect: !carriesNoControl && built.UsedPrefab ? caption?.rectTransform : null,
			controlRect: controlRect,
			selectionGraphic: button?.targetGraphic as Image)
		{
			MissedGamePrefab = built.MissedPrefab,
			FixedPointerSurface = pointerFixed,
		};

		view.Wire();
		view.Apply(element);
		return view;
	}

	/// <summary>
	/// Applies one frame's element, writing only what differs from the last application.
	/// <paramref name="aloneOnItsLine"/> is the model's own shape: an element that is the only one on its line
	/// takes that line (a dropdown or a field IS one of the game's rows — a label with a control beside it),
	/// while one that shares its line with another element keeps its own size.
	/// </summary>
	internal void Apply(OnlineUiElementModel element, bool aloneOnItsLine = false)
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

		ApplyLayout(element, aloneOnItsLine);
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

		// A coloured caption alone is a thin signal for "this is the page you are on": the game's own sprite
		// takes a wash on the current choice too (the tab row and the transport switch both mark one). A
		// colour block's graphic IS its colour, so it is left alone.
		if (_selectionGraphic != null && _selectionGraphic != _swatch)
		{
			_selectionGraphic.color = element.Selected ? SelectedFill : UnselectedFill;
		}
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

	/// <summary>The colour block's fill, written only when it changed: the game's own control keeps its
	/// sprite, its 9-slice and its size, so the block reads as one of the game's rows.</summary>
	private void ApplySwatchColor()
	{
		if (_swatch != null)
		{
			_swatch.color = ToColor(_color);
		}
	}

	/// <summary>
	/// The layout half of one element, holding no size of its own: the width is declared beside this — from the
	/// model's floor and the game's own authored size (<see cref="OnlineUiControlSizing"/> and
	/// <see cref="OnlineUiRowGeometry"/>), never measured from content that stretches inside the box — and this
	/// keeps the two things a control's own group must know: a label is flexible (it takes what its line has
	/// left) and every control of a page is the compact height.
	/// </summary>
	private void ApplyLayout(OnlineUiElementModel element, bool aloneOnItsLine)
	{
		// A label always takes what its line has left; one of the game's own rows takes the line only when it is
		// ALONE on it. Sharing a line, a field or a dropdown keeps its own size instead of splitting the
		// surplus with the label beside it — which is what the controls' own floors are for.
		var flexible = element.Kind == OnlineUiElementKind.Label
			|| (aloneOnItsLine && OnlineUiControlSizing.FillsTheRow(element)) ? 1f : 0f;
		if (!_layout.flexibleWidth.Equals(flexible))
		{
			_layout.flexibleWidth = flexible;
		}

		if (element.Kind == OnlineUiElementKind.Label)
		{
			// A label's height is its own wrapped text, and TMP is the layout element that knows it: the
			// LayoutElement keeps only a one-line floor. The fixed one-line height this used to be is what
			// made a hint that wrapped paint over the row below it.
			_layout.preferredHeight = -1f;
			_layout.minHeight = _typography.Size + OnlineUiControlFactory.LabelHeightPadding;
			return;
		}

		if (!_layout.preferredHeight.Equals(OnlineUiWindowLayout.ControlHeight))
		{
			_layout.preferredHeight = OnlineUiWindowLayout.ControlHeight;
			_layout.minHeight = OnlineUiWindowLayout.ControlHeight;
		}
	}

	/// <summary>
	/// The row's own parts, laid out inside it: a dropdown, a field, a slider and a toggle ARE one of the
	/// game's own rows, so their inside is CUO's geometry rather than the game's own placement.
	/// <paramref name="controlWidth"/> is the width this control takes on its line — already decided
	/// (<see cref="OnlineUiControlSizing"/>), never measured from the control's own stretched content.
	/// </summary>
	internal void LayOutRow(float controlWidth)
	{
		if (_controlRect is null)
		{
			return;
		}

		OnlineUiRowGeometry.LayOutControlRow(
			_root,
			_labelRect,
			_controlRect,
			controlWidth,
			OnlineUiWindowLayout.ControlHeight,
			_valueText != null ? _valueText.rectTransform : null);
	}

	/// <summary>
	/// Hands this control's dropdown template to the window's popup layer: a <c>TMP_Dropdown</c> builds its
	/// list out of that template, so a template inside CUO's page is a list clipped by the page's mask and
	/// drawn under the rows after it — the report of options behind the window that take no click.
	/// </summary>
	internal void AdoptPopup(OnlineUiDropdownPopup popup)
	{
		if (_dropdown != null)
		{
			popup.Adopt(_dropdown);
		}
	}

	/// <summary>
	/// The width this control takes on its line. It is a DECLARATION, never a measurement of the control's own
	/// content: the model's hint is a floor, a caption's own text (which TMP measures from the text rather than
	/// from the rect) raises it for the kinds whose width the game's row does not author, and the game's own
	/// prefab answers for the rest. Content that stretches inside the box reports the box, which is the feedback
	/// the acceptance pass of batch `20261001-k` caught growing without bound.
	/// </summary>
	internal float EffectiveWidth(OnlineUiElementModel element) => element.Kind switch
	{
		OnlineUiElementKind.Label => 0f,
		OnlineUiElementKind.Button or OnlineUiElementKind.ColorSwatch =>
			OnlineUiControlBox.EffectiveWidth(element.Width, CaptionWidth + (2f * _typography.Size)),
		OnlineUiElementKind.TextField or OnlineUiElementKind.Dropdown =>
			OnlineUiControlBox.EffectiveWidth(OnlineUiControlSizing.WithMinimum(element.Width), 0f),
		_ => OnlineUiControlBox.EffectiveWidth(element.Width, OnlineUiControlSizing.PrefabWidth(_rect)),
	};

	/// <summary>The width the control's own caption needs — 0 for a kind whose caption is not its content (a
	/// colour block's caption is empty, and a field's row label is not the value it holds).</summary>
	private float CaptionWidth => _caption != null ? OnlineUiControlSizing.CaptionWidth(_caption) : 0f;

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
