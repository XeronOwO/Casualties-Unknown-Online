using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The Online UI window's page builder: what a page drawer writes to instead of drawing (ticket
/// online-ui-art-and-controls-overhaul, S2b). A drawer appends rows and controls, and every interactive
/// control registers the action its id carries — the model goes to the game's own surface as a value,
/// and the intent that comes back is dispatched to the action registered under the same id.
///
/// <para>
/// This is the one place that knows the pairing, and it exists because both halves live in the plugin:
/// the <see cref="OnlineUiContext"/> the drawers already read, and the action delegates the previous
/// IMGUI controls called inline. Ids are rebuilt together with the model on every frame the window is
/// open, so an intent from the frame before always finds the action that produced its control; an id
/// that is gone by the time the intent arrives is dropped by the window, never guessed at.
/// </para>
/// </summary>
internal sealed class OnlineUiPageBuilder
{
	private readonly Dictionary<string, Action<OnlineUiIntent>> _actions;
	private readonly List<OnlineUiRowModel> _rows = [];
	private readonly List<OnlineUiElementModel> _tabs = [];

	internal OnlineUiPageBuilder(OnlineUiContext context, Dictionary<string, Action<OnlineUiIntent>> actions)
	{
		Context = context;
		_actions = actions;
	}

	/// <summary>The runtime facts and action delegates the drawers read.</summary>
	internal OnlineUiContext Context { get; }

	/// <summary>The window's tab row, in page order.</summary>
	internal IReadOnlyList<OnlineUiElementModel> Tabs => _tabs;

	/// <summary>The page's rows, in the order they were added.</summary>
	internal IReadOnlyList<OnlineUiRowModel> Rows => _rows;

	/// <summary>The catalogue lookup, so a drawer does not spell <c>Context.T</c> at every line.</summary>
	internal string T(string key) => Context.T(key);

	/// <summary>The formatted catalogue lookup, so a drawer does not spell <c>Context.F</c> at every line.</summary>
	internal string F(string key, params object?[] args) => Context.F(key, args);

	/// <summary>
	/// The width one tab asks for, so six of them fit one row of the window's shell. The value is a hint
	/// in the window's canvas units like every other width here; without it each tab would take the game
	/// row prefab's own (much wider) size and the tab row would overflow.
	/// </summary>
	internal const float TabWidth = 112f;

	/// <summary>Adds one tab to the window's tab row; the click selects <paramref name="select"/>'s page.</summary>
	internal void Tab(string id, string label, bool selected, Action select)
	{
		_actions[id] = _ => select();
		_tabs.Add(OnlineUiElementModel.Button(id, label, width: TabWidth, selected: selected));
	}

	/// <summary>One row holding <paramref name="elements"/>, in order.</summary>
	internal void Row(params OnlineUiElementModel[] elements) => _rows.Add(OnlineUiRowModel.Of(elements));

	/// <summary>A vertical gap, so a page keeps the breathing room its IMGUI rows had.</summary>
	internal void Space() => _rows.Add(new OnlineUiRowModel([]));

	/// <summary>A full-width line of body text.</summary>
	internal void Label(string text) =>
		Row(OnlineUiElementModel.Label(text, OnlineUiTextStyle.Default, OnlineUiTheme.ToRgba(OnlineUiTheme.Text)));

	/// <summary>A full-width line of secondary text (hints, small print).</summary>
	internal void Muted(string text) =>
		Row(OnlineUiElementModel.Label(text, OnlineUiTextStyle.Muted, OnlineUiTheme.ToRgba(OnlineUiTheme.Muted)));

	/// <summary>A section heading: what the rows below it belong to.</summary>
	internal void Section(string text) =>
		Row(OnlineUiElementModel.Label(text, OnlineUiTextStyle.Section, OnlineUiTheme.ToRgba(OnlineUiTheme.Accent)));

	/// <summary>A line of status text in one of the theme's status colours.</summary>
	internal void Status(string text, Color color) =>
		Row(OnlineUiElementModel.Label(text, OnlineUiTextStyle.Default, OnlineUiTheme.ToRgba(color)));

	/// <summary>A push button; <paramref name="clicked"/> runs when its id comes back as an intent.</summary>
	internal void Button(string id, string text, Action clicked, float width = 0f, bool selected = false)
	{
		_actions[id] = _ => clicked();
		_rows.Add(new OnlineUiRowModel([OnlineUiElementModel.Button(id, text, width, selected)]));
	}

	/// <summary>A push button appended to <paramref name="elements"/>, for a row built by the caller.
	/// <paramref name="selected"/> marks the current choice of a small switch drawn as buttons (the Home
	/// page's transport selector), the way the tab row marks the current page.</summary>
	internal OnlineUiElementModel ButtonElement(
		string id,
		string text,
		Action clicked,
		float width = 0f,
		bool selected = false)
	{
		_actions[id] = _ => clicked();
		return OnlineUiElementModel.Button(id, text, width, selected);
	}

	/// <summary>A label appended to <paramref name="elements"/>, for a row built by the caller.</summary>
	internal OnlineUiElementModel LabelElement(string text, OnlineUiTextStyle style = OnlineUiTextStyle.Default, Color? color = null) =>
		OnlineUiElementModel.Label(text, style, color is { } value ? OnlineUiTheme.ToRgba(value) : null);

	/// <summary>A text field appended to <paramref name="elements"/>, for a row built by the caller.</summary>
	internal OnlineUiElementModel TextFieldElement(string id, string label, string value, int maxLength, Action<string> edited, float width = 0f)
	{
		_actions[id] = intent => edited(intent.Text);
		return OnlineUiElementModel.TextField(id, label, value, width, maxLength);
	}

	/// <summary>A checkbox with its caption; <paramref name="toggled"/> gets the value it now holds.</summary>
	internal void Toggle(string id, string text, bool value, Action<bool> toggled)
	{
		_actions[id] = intent => toggled(intent.Flag);
		_rows.Add(new OnlineUiRowModel([OnlineUiElementModel.Toggle(id, text, value)]));
	}

	/// <summary>A row of label plus dropdown; <paramref name="selected"/> gets the chosen option's index.</summary>
	internal void Dropdown(
		string id,
		string label,
		IReadOnlyList<string> options,
		int optionIndex,
		Action<int> selected,
		float width = 0f)
	{
		_actions[id] = intent => selected(intent.Index);
		_rows.Add(new OnlineUiRowModel([OnlineUiElementModel.Dropdown(id, label, options, optionIndex, width)]));
	}

	/// <summary>A row of label plus slider; <paramref name="changed"/> gets the value it now holds.</summary>
	internal void Slider(
		string id,
		string label,
		float value,
		float minimum,
		float maximum,
		string valueText,
		Action<float> changed,
		float width = 0f)
	{
		_actions[id] = intent => changed(intent.Number);
		_rows.Add(new OnlineUiRowModel([OnlineUiElementModel.Slider(id, label, value, minimum, maximum, valueText, width)]));
	}
}
