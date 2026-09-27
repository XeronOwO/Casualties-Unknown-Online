using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One element of the Online UI window's display list (ticket online-ui-art-and-controls-overhaul, S2b).
/// A plain value like everything else that crosses the adapter boundary: the plugin builds it from the
/// runtime facts it already reads, and the adapter puts it on the game's own control — no Unity object
/// travels either way, and no judgement is made here.
///
/// <para>
/// One record carries every kind because the kinds are one family with one reconcile path on the far
/// side (match by <see cref="Id"/>, write only what changed), and because a flat value compares cheaply
/// per frame. Each field means something for some kinds and nothing for the others; the factories below
/// are how call sites are written, so a row never spells the unused fields out.
/// </para>
///
/// <para>Per kind, the fields that carry meaning:</para>
/// <list type="bullet">
/// <item><description><see cref="OnlineUiElementKind.Label"/> — <c>Text</c>, <c>Style</c>, <c>Color</c>
/// (null keeps the game's own label colour), <c>Width</c> (0 = the row's remaining width).</description></item>
/// <item><description><see cref="OnlineUiElementKind.Button"/> — <c>Id</c>, <c>Text</c>, <c>Width</c>,
/// <c>Selected</c> (the tab row's current page, the Home page's transport switch).</description></item>
/// <item><description><see cref="OnlineUiElementKind.TextField"/> — <c>Id</c>, <c>Text</c> (the row's
/// label), <c>Value</c>, <c>Width</c>, <c>MaxLength</c>.</description></item>
/// <item><description><see cref="OnlineUiElementKind.Toggle"/> — <c>Id</c>, <c>Text</c> (the caption),
/// <c>Flag</c>.</description></item>
/// <item><description><see cref="OnlineUiElementKind.Dropdown"/> — <c>Id</c>, <c>Text</c> (the row's
/// label), <c>Options</c>, <c>OptionIndex</c>, <c>Width</c>.</description></item>
/// <item><description><see cref="OnlineUiElementKind.Slider"/> — <c>Id</c>, <c>Text</c> (the row's
/// label), <c>Number</c>, <c>Minimum</c>, <c>Maximum</c>, <c>Value</c> (the formatted value shown beside
/// the slider), <c>Width</c>.</description></item>
/// <item><description><see cref="OnlineUiElementKind.ColorSwatch"/> — <c>Id</c> (EMPTY = a preview that
/// reports nothing), <c>Color</c> (the block's own fill), <c>Width</c>.</description></item>
/// </list>
///
/// <para>
/// <see cref="Width"/> is a FLOOR in the canvas units the window is laid out in, not a pixel size and not a
/// ceiling: the adapter asks the control what its own content needs — a caption measured at the game's own
/// font, a dropdown's widest option — and takes the wider of the two, because the same label does not fit in
/// the same box in two languages (ticket online-ui-layout-and-input-detail-pass, S2). The wrap is the
/// Runtime's rule (<see cref="OnlineUiRowLayout.LineOf(IReadOnlyList{float}, float, float)"/>), asked with
/// the width each control actually takes; 0 means "no floor" — for a control built on one of the game's own
/// row prefabs that is the prefab's own size, and for a label it is "whatever the row has left".
/// </para>
/// </summary>
public readonly record struct OnlineUiElementModel(
	OnlineUiElementKind Kind,
	string Id,
	string Text,
	OnlineUiTextStyle Style,
	OnlineUiNativeRgba? Color,
	float Width,
	bool Selected,
	string Value,
	float Number,
	float Minimum,
	float Maximum,
	bool Flag,
	int OptionIndex,
	int MaxLength,
	IReadOnlyList<string>? Options)
{
	/// <summary>A line of text. <paramref name="width"/> 0 lets the label take the row's remaining width.</summary>
	public static OnlineUiElementModel Label(
		string text,
		OnlineUiTextStyle style = OnlineUiTextStyle.Default,
		OnlineUiNativeRgba? color = null,
		float width = 0f) =>
		new(
			OnlineUiElementKind.Label,
			Id: "",
			Text: text,
			Style: style,
			Color: color,
			Width: width,
			Selected: false,
			Value: "",
			Number: 0f,
			Minimum: 0f,
			Maximum: 0f,
			Flag: false,
			OptionIndex: -1,
			MaxLength: 0,
			Options: null);

	/// <summary>A push button; <paramref name="id"/> is what its click comes back as.</summary>
	public static OnlineUiElementModel Button(
		string id,
		string text,
		float width = 0f,
		bool selected = false) =>
		new(
			OnlineUiElementKind.Button,
			Id: id,
			Text: text,
			Style: OnlineUiTextStyle.Default,
			Color: null,
			Width: width,
			Selected: selected,
			Value: "",
			Number: 0f,
			Minimum: 0f,
			Maximum: 0f,
			Flag: false,
			OptionIndex: -1,
			MaxLength: 0,
			Options: null);

	/// <summary>A row of label plus text field. <paramref name="maxLength"/> 0 means no character limit.</summary>
	public static OnlineUiElementModel TextField(
		string id,
		string label,
		string value,
		float width = 0f,
		int maxLength = 0) =>
		new(
			OnlineUiElementKind.TextField,
			Id: id,
			Text: label,
			Style: OnlineUiTextStyle.Default,
			Color: null,
			Width: width,
			Selected: false,
			Value: value,
			Number: 0f,
			Minimum: 0f,
			Maximum: 0f,
			Flag: false,
			OptionIndex: -1,
			MaxLength: maxLength,
			Options: null);

	/// <summary>A checkbox with its caption.</summary>
	public static OnlineUiElementModel Toggle(
		string id,
		string text,
		bool value,
		float width = 0f) =>
		new(
			OnlineUiElementKind.Toggle,
			Id: id,
			Text: text,
			Style: OnlineUiTextStyle.Default,
			Color: null,
			Width: width,
			Selected: false,
			Value: "",
			Number: 0f,
			Minimum: 0f,
			Maximum: 0f,
			Flag: value,
			OptionIndex: -1,
			MaxLength: 0,
			Options: null);

	/// <summary>A row of label plus dropdown over <paramref name="options"/>.</summary>
	public static OnlineUiElementModel Dropdown(
		string id,
		string label,
		IReadOnlyList<string> options,
		int optionIndex,
		float width = 0f) =>
		new(
			OnlineUiElementKind.Dropdown,
			Id: id,
			Text: label,
			Style: OnlineUiTextStyle.Default,
			Color: null,
			Width: width,
			Selected: false,
			Value: "",
			Number: 0f,
			Minimum: 0f,
			Maximum: 0f,
			Flag: false,
			OptionIndex: optionIndex,
			MaxLength: 0,
			Options: options);

	/// <summary>A row of label plus slider and its formatted value.</summary>
	public static OnlineUiElementModel Slider(
		string id,
		string label,
		float value,
		float minimum,
		float maximum,
		string valueText,
		float width = 0f) =>
		new(
			OnlineUiElementKind.Slider,
			Id: id,
			Text: label,
			Style: OnlineUiTextStyle.Default,
			Color: null,
			Width: width,
			Selected: false,
			Value: valueText,
			Number: value,
			Minimum: minimum,
			Maximum: maximum,
			Flag: false,
			OptionIndex: -1,
			MaxLength: 0,
			Options: null);

	/// <summary>
	/// A block of <paramref name="color"/>. A non-empty <paramref name="id"/> makes it a swatch the player
	/// clicks, and that click arrives as that id; an EMPTY id makes it a preview of the colour the player
	/// carries now, which is nothing to click and reports nothing — the same way a label carries no id.
	/// </summary>
	public static OnlineUiElementModel ColorSwatch(
		string id,
		OnlineUiNativeRgba color,
		float width = 0f) =>
		new(
			OnlineUiElementKind.ColorSwatch,
			Id: id,
			Text: "",
			Style: OnlineUiTextStyle.Default,
			Color: color,
			Width: width,
			Selected: false,
			Value: "",
			Number: 0f,
			Minimum: 0f,
			Maximum: 0f,
			Flag: false,
			OptionIndex: -1,
			MaxLength: 0,
			Options: null);
}
