using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// How one element of a page is WRITTEN onto the game's own control (tickets
/// online-ui-art-and-controls-overhaul S2b, online-ui-paired-controls-and-page-scroll S2): the caption, the
/// selected wash, and the value of every kind the player can set — a dropdown's option, a field's text, a
/// number, a slider's position and a colour block's fill.
///
/// <para>
/// It is a type of its own because the view and this answer different questions. <see cref="OnlineUiControlView"/>
/// owns the object graph — which prefab was built, which parts were found, where they are placed and what the
/// player's gesture reports — while everything here is about the ELEMENT: which of the model's fields means
/// something for this kind, what counts as a change, and which of the two directions a value travels in. That
/// is also where each half's state lives (<c>_text</c>, <c>_value</c>, <c>_number</c>, <c>_index</c> are this
/// type's own), so a write can be skipped without asking the view what it wrote last.
/// </para>
///
/// <para>
/// One rule runs through all of it: the MODEL is the one source of a value, so every write from the model
/// uses uGUI's <c>WithoutNotify</c> form (echoing a model value back as a player action would report the
/// element's own state), and a field the player is inside is never written at all.
/// </para>
/// </summary>
internal sealed class OnlineUiControlFields
{
	/// <summary>The caption colour of the current choice (a tab, the transport switch) and of the others.</summary>
	private static readonly Color SelectedCaption = new(0.85f, 0.72f, 0.38f, 1f);

	private static readonly Color UnselectedCaption = new(0.92f, 0.93f, 0.94f, 1f);

	/// <summary>The game's own sprite colour for a button that is not the current choice: untinted.</summary>
	private static readonly Color UnselectedFill = Color.white;

	/// <summary>A warm wash over the game's own sprite for the current choice, so the open page and the
	/// transport the session uses are visible at a glance and not only in the caption.</summary>
	private static readonly Color SelectedFill = new(1f, 0.86f, 0.6f, 1f);

	private readonly TextMeshProUGUI? _caption;
	private readonly TextMeshProUGUI? _rowLabel;
	private readonly TextMeshProUGUI? _valueText;

	/// <summary>The graphic a selected button tints: the tab row's current page and the Home page's transport
	/// switch read as chosen, not only as a differently coloured caption.</summary>
	private readonly Image? _selectionGraphic;

	/// <summary>The image a colour block tints; only a <see cref="OnlineUiElementKind.ColorSwatch"/> has one.</summary>
	private readonly Image? _swatch;

	private readonly Toggle? _toggle;
	private readonly TMP_Dropdown? _dropdown;
	private readonly TMP_InputField? _input;
	private readonly TMP_InputField? _numberInput;
	private readonly Slider? _slider;
	private readonly OnlineUiControlTypography _typography;

	private string _text = "\0";
	private string _value = "\0";

	/// <summary>The last text a NUMERIC field was given: its own write-back record, because
	/// <see cref="_value"/> carries a row's LABEL for the other field kind (ticket
	/// online-ui-paired-controls-and-page-scroll, S2).</summary>
	private string _numberText = "\0";
	private bool _selected;
	private bool _flag;
	private float _number = float.NaN;
	private float _minimum = float.NaN;
	private float _maximum = float.NaN;
	private int _index = -1;
	private int _optionCount = -1;
	private OnlineUiNativeRgba? _color;

	internal OnlineUiControlFields(
		OnlineUiControlTypography typography,
		TextMeshProUGUI? caption,
		TextMeshProUGUI? rowLabel,
		TextMeshProUGUI? valueText,
		Image? selectionGraphic,
		Image? swatch,
		Toggle? toggle,
		TMP_Dropdown? dropdown,
		TMP_InputField? input,
		TMP_InputField? numberInput,
		Slider? slider)
	{
		_typography = typography;
		_caption = caption;
		_rowLabel = rowLabel;
		_valueText = valueText;
		_selectionGraphic = selectionGraphic;
		_swatch = swatch;
		_toggle = toggle;
		_dropdown = dropdown;
		_input = input;
		_numberInput = numberInput;
		_slider = slider;
	}

	/// <summary>
	/// Writes one frame's element, skipping every part that did not change. <paramref name="kindChanged"/> is
	/// the view's fact — the element this control was built for is not the element it now carries — and it
	/// forces every part, because nothing this type remembers about the old kind means anything for the new
	/// one.
	/// </summary>
	internal void Apply(OnlineUiElementModel element, bool kindChanged)
	{
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
			case OnlineUiElementKind.NumberField:
				ApplyNumberField(element, kindChanged);
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
					_caption.fontStyle = OnlineUiControlTypography.WeightOf(element.Style);
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
		if (_caption == null || _selectionGraphic == null)
		{
			return;
		}

		_caption.color = element.Selected ? SelectedCaption : UnselectedCaption;

		// A coloured caption alone is a thin signal for "this is the page you are on": the game's own sprite
		// takes a wash on the current choice too (the tab row and the transport switch both mark one). A
		// colour block's graphic IS its colour, so it is left alone.
		if (_selectionGraphic != _swatch)
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

	/// <summary>
	/// A numeric field: the text the model holds, written when the player is not in it. Nothing is parsed
	/// here — a value that is not a number, or one outside the range, is the caller's to refuse
	/// (<see cref="OnlineUiIntentKind.ControlCommitted"/>), which keeps the policy in one place instead of
	/// one per view.
	/// </summary>
	private void ApplyNumberField(OnlineUiElementModel element, bool kindChanged)
	{
		if (_numberInput is not { } input)
		{
			return;
		}

		if (kindChanged)
		{
			// Standard rather than a numeric content type: TMP's own numeric filter drops `-` and `.` as
			// they are typed, and a range that reaches below zero is one the player has to be able to write
			// (OnlineUiNumberField is the rule that judges the result).
			input.contentType = TMP_InputField.ContentType.Standard;
			input.lineType = TMP_InputField.LineType.SingleLine;
			input.characterLimit = element.MaxLength;
			input.richText = false;
		}

		// Never while the player is in the field: the model carries the value the plugin holds, and writing
		// it under the caret would fight the typing it is waiting for.
		if (!input.isFocused && _numberText != element.Value)
		{
			_numberText = element.Value;
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
			// WithoutNotify writes the value and lets the engine draw it: uGUI's own slider marks its handle
			// and its fill for a rebuild on the next frame's late update, which is the frame the player sees
			// the field and the track agree in (ticket online-ui-paired-controls-and-page-scroll, S2).
			slider.SetValueWithoutNotify(element.Number);
		}

		if (_valueText != null && (kindChanged || _value != element.Value))
		{
			_value = element.Value;
			_valueText.text = element.Value;
		}
	}

	/// <summary>The colour block's fill, written only when it changed. The fill sits INSIDE the game's own row
	/// sprite, so the block keeps the game's art as its frame and shows the colour across its whole middle
	/// (see <see cref="OnlineUiControlFactory.SwatchFillOn"/>).</summary>
	private void ApplySwatchColor()
	{
		if (_swatch != null)
		{
			_swatch.color = ToColor(_color);
		}
	}

	/// <summary>
	/// Puts a field back to the number the model holds when the player leaves it without committing: nothing
	/// was decided, so nothing stays on screen that the model does not say — a value refused for being out of
	/// range or not a number is visibly gone rather than silently kept as text. It runs on the field's own
	/// deselect event, which uGUI raises for a lost focus as well as for Enter.
	/// </summary>
	internal void ReturnToModel()
	{
		if (_numberInput is { } input && _numberText.Length > 0 && input.text != _numberText)
		{
			input.SetTextWithoutNotify(_numberText);
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
