using System;
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
	private readonly GameObject _root;
	private readonly RectTransform _rect;
	private readonly LayoutElement _layout;
	private readonly Action<OnlineUiIntent> _report;
	private readonly OnlineUiControlTypography _typography;
	private readonly Button? _button;

	/// <summary>What this element's own value is written with: the caption, the selected wash and the value of
	/// every kind the player can set (<see cref="OnlineUiControlFields"/>, which also keeps the state each of
	/// those writes compares against).</summary>
	private readonly OnlineUiControlFields _fields;

	/// <summary>The controls this view WIRES and lends to other layers rather than writes: the checkbox and the
	/// slider report their own gestures, the dropdown's template rides the window's popup layer, and the
	/// caption is what the width rule measures. What each of them SHOWS belongs to
	/// <see cref="OnlineUiControlFields"/>.</summary>
	private readonly Toggle? _toggle;

	private readonly TMP_Dropdown? _dropdown;
	private readonly TMP_InputField? _input;
	private readonly TMP_InputField? _numberInput;
	private readonly Slider? _slider;
	private readonly TextMeshProUGUI? _caption;
	private readonly TextMeshProUGUI? _rowLabel;
	private readonly TextMeshProUGUI? _valueText;

	/// <summary>The game's own row label (child 0), when the prefab carries one: the geometry inside the row is
	/// CUO's, and this is the half of it that takes what the control does not.</summary>
	private readonly RectTransform? _labelRect;

	/// <summary>The control the row carries on child 1, sized by CUO inside the row it belongs to.</summary>
	private readonly RectTransform? _controlRect;

	/// <summary>
	/// The width the game's own prefab authored for that control, read ONCE when the view is built — the one
	/// moment the prefab's own rect is still intact, because the row's layout group writes it from the first
	/// pass on (<see cref="OnlineUiControlSizing.AuthoredChildWidth"/>). It is what a toggle and a slider are
	/// sized by; the element root is never asked, because its own preferred width contains this control's.
	/// </summary>
	private readonly float _controlAuthoredWidth;

	private OnlineUiElementKind _kind;
	private string _id = "";

	private OnlineUiControlView(
		GameObject root,
		RectTransform rect,
		LayoutElement layout,
		OnlineUiControlTypography typography,
		Action<OnlineUiIntent> report,
		Button? button,
		Image? swatch,
		Toggle? toggle,
		TMP_Dropdown? dropdown,
		TMP_InputField? input,
		TMP_InputField? numberInput,
		Slider? slider,
		TextMeshProUGUI? caption,
		TextMeshProUGUI? rowLabel,
		TextMeshProUGUI? valueText,
		RectTransform? labelRect,
		RectTransform? controlRect,
		float controlAuthoredWidth,
		Image? selectionGraphic)
	{
		_root = root;
		_rect = rect;
		_layout = layout;
		_report = report;
		_typography = typography;
		_button = button;
		_toggle = toggle;
		_dropdown = dropdown;
		_input = input;
		_numberInput = numberInput;
		_slider = slider;
		_caption = caption;
		_rowLabel = rowLabel;
		_valueText = valueText;
		_labelRect = labelRect;
		_controlRect = controlRect;
		_controlAuthoredWidth = controlAuthoredWidth;
		_fields = new OnlineUiControlFields(
			typography,
			caption,
			rowLabel,
			valueText,
			selectionGraphic,
			swatch,
			toggle,
			dropdown,
			input,
			numberInput,
			slider);
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

	/// <summary>
	/// Whether this element's own box is the LINE the model sent its control to (ticket
	/// online-ui-paired-controls-and-page-scroll, S3): true for a paired element whose prefab carries the
	/// control it names. The surface asks this before it lays the elements the model put beside the control
	/// out, because those belong INSIDE this row — that is what makes the row the page's own width and puts
	/// the value field bound to the slider at the row's trailing edge. False for a paired element whose
	/// prefab could not be loaded: the placeholder has no control to hold anything beside.
	/// </summary>
	internal bool OwnsItsLine { get; private set; }

	/// <summary>Builds the view for one element under <paramref name="parent"/> on the object
	/// <see cref="OnlineUiControlFactory"/> produces for its kind, and finds the parts this view drives.</summary>
	internal static OnlineUiControlView Create(
		OnlineUiElementModel element,
		Transform parent,
		OnlineUiControlTypography typography,
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
		var numberInput = element.Kind == OnlineUiElementKind.NumberField ? input : null;
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
		if (control != null && element.Kind is OnlineUiElementKind.Dropdown or OnlineUiElementKind.TextField or OnlineUiElementKind.NumberField)
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
			swatch: element.Kind == OnlineUiElementKind.ColorSwatch ? OnlineUiControlFactory.SwatchFillOn(root, button) : null,
			toggle: toggle,
			dropdown: dropdown,
			input: element.Kind == OnlineUiElementKind.TextField ? input : null,
			numberInput: numberInput,
			slider: slider,
			caption: caption,
			rowLabel: element.Kind == OnlineUiElementKind.Label ? null : caption,
			valueText: element.Kind == OnlineUiElementKind.Slider && built.UsedPrefab ? OnlineUiControlFactory.ChildText(root.transform, 2) : null,
			labelRect: !carriesNoControl && built.UsedPrefab ? caption?.rectTransform : null,
			controlRect: controlRect,
			controlAuthoredWidth: OnlineUiControlSizing.AuthoredChildWidth(controlRect),
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

		_fields.Apply(element, kindChanged);
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

		if (_numberInput != null)
		{
			// A number is COMMITTED rather than announced at every keystroke: while the player is typing,
			// `-`, `0.` and `0.8` are all "not a number yet", and half of one is not a value anybody meant
			// (the hex field's own rule). The text as it stands is what the caller judges.
			_numberInput.onEndEdit.AddListener(CommitNumber);
			_numberInput.onDeselect.AddListener(_ => ReturnToModel());
		}
	}

	/// <summary>
	/// Hands one numeric field's contents back as a commit, but only when it holds something: uGUI raises
	/// the same event for a focused field's Enter that it raises for a lost focus, and an empty event would
	/// report the element's own state as a player action.
	/// </summary>
	private void CommitNumber(string text)
	{
		if (text.Length > 0)
		{
			_report(new OnlineUiIntent(OnlineUiIntentKind.ControlCommitted, _id, Text: text));
		}
	}

	/// <summary>
	/// Puts the field back to the number the model holds when the player leaves it without committing:
	/// nothing was decided, so nothing stays on screen that the model does not say — a value refused for
	/// being out of range or not a number is visibly gone rather than silently kept as text.
	/// </summary>
	private void ReturnToModel() => _fields.ReturnToModel();

	/// <summary>
	/// The layout half of one element, holding no size of its own: the width is declared beside this — from the
	/// model's floor and the game's own authored size (<see cref="OnlineUiControlSizing"/> and
	/// <see cref="OnlineUiRowGeometry"/>), never measured from content that stretches inside the box — and this
	/// keeps the two things a control's own group must know: a label is flexible (it takes what its line has
	/// left) and every control of a page is the compact height.
	/// </summary>
	private void ApplyLayout(OnlineUiElementModel element, bool aloneOnItsLine)
	{
		// A label always takes what its line has left; so does the element that is the only one on its line, and
		// the control of a PAIRED row — its line is its own whether or not the model put anything beside it, and
		// what it did put there is laid out inside this row (S3). Sharing a line, a field or a dropdown keeps its
		// own size instead of splitting the surplus with the label beside it — which is what the controls' own
		// floors are for.
		var flexible = element.Kind == OnlineUiElementKind.Label
			|| ((aloneOnItsLine || element.NewLine) && OnlineUiControlSizing.FillsTheRow(element)) ? 1f : 0f;
		if (!_layout.flexibleWidth.Equals(flexible))
		{
			_layout.flexibleWidth = flexible;
		}

		if (element.Kind == OnlineUiElementKind.Label)
		{
			// A label's height is its own wrapped text, and TMP is the layout element that knows it: the
			// LayoutElement keeps only a one-line floor. The fixed one-line height this used to be is what
			// made a hint that wrapped paint over the row below it.
			ApplyRowRoomToText(_caption);
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
	/// Gives a page's own line of text the room every row's label keeps from its row's own edge
	/// (<see cref="OnlineUiRowGeometry.RowPadding"/>): a name that is an element of its own — the line above a
	/// paired control — then starts where the labels of the rows start, and not at the page's own edge (the
	/// user's fourth point of 2026-10-15).
	///
	/// <para>
	/// The room is a MARGIN on the text rather than a narrower box, because the box IS the line the label was
	/// wrapped onto: a width written instead would move the label off its own line, and a group that wrote one
	/// would fight the wrap that produced that line. The write is guarded because TMP re-draws its text when
	/// the margin changes, and this runs once per frame per label.
	/// </para>
	/// </summary>
	private static void ApplyRowRoomToText(TMP_Text? text)
	{
		if (text is null)
		{
			return;
		}

		var margin = text.margin;
		if (!margin.x.Equals(OnlineUiRowGeometry.RowPadding))
		{
			text.margin = new Vector4(OnlineUiRowGeometry.RowPadding, margin.y, margin.z, margin.w);
		}
	}

	/// <summary>
	/// Lays this element out on the line the window wrapped it into: the rule is
	/// <see cref="OnlineUiRowGeometry.LayOutRow"/>'s, and this hands it the parts of this element — its own
	/// object, the layout element that carries its size, and the label and the control the game's row
	/// shipped. The formatted value a slider's row prefab carries is NOT part of it any more: it is the
	/// numeric field beside the track, laid out with the slider (<see cref="OnlineUiSliderGeometry"/>).
	///
	/// <para>
	/// A PAIRED element (<see cref="OnlineUiElementModel.NewLine"/>) owns the line the model sent its control
	/// to instead of sharing one: its box is that line's whole width and the elements the model put beside the
	/// control are laid out inside it, which is the shape the user asked for on 2026-10-15 (ticket
	/// online-ui-paired-controls-and-page-scroll, S3). <see cref="OwnsItsLine"/> is this frame's answer to
	/// whether that holds, and the surface asks it before it places those elements.
	/// </para>
	/// </summary>
	internal void LayOutRow(OnlineUiElementModel element, float controlWidth, float columnWidth)
	{
		var prefabValue = _valueText != null ? _valueText.rectTransform : null;

		// A row whose prefab carries no control holds nothing beside it — it is the placeholder a moved prefab
		// leaves — so the answer follows the control the geometry is handed, in the same breath.
		OwnsItsLine = element.NewLine && _controlRect != null;
		if (element.NewLine)
		{
			OnlineUiRowGeometry.LayOutPairedRow(_root, _layout, _labelRect, _controlRect, controlWidth, columnWidth, prefabValue);
			return;
		}

		OnlineUiRowGeometry.LayOutRow(
			_root,
			_layout,
			_labelRect,
			_controlRect,
			controlWidth,
			columnWidth,
			prefabValue);
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
	/// The width this control takes on its line. The rule is <see cref="OnlineUiControlSizing"/>'s, together
	/// with every other width of this surface, so the wrap the window asks and the width the element is laid out
	/// at cannot drift apart; what only this view knows is what it hands over — the caption the game's row
	/// shipped, the game's own type size, and the width the game authored for this control, read once when the
	/// view was built.
	/// </summary>
	internal float EffectiveWidth(OnlineUiElementModel element) =>
		OnlineUiControlSizing.EffectiveWidth(element, _caption, _typography.Size, _controlAuthoredWidth);

	/// <summary>
	/// The least width this control can be given without cutting its caption — the width rule's own floor for
	/// the element (<see cref="OnlineUiControlSizing.MinimumWidth"/>), read by the one caller that has to share
	/// a fixed band among several controls: the window's tab strip, whose six English captions need more room
	/// than the strip has (batch `20261015-a`).
	/// </summary>
	internal float MinimumWidth(OnlineUiElementModel element) =>
		OnlineUiControlSizing.MinimumWidth(element, _caption);

	/// <summary>
	/// The width this element's NAME needs, for the page's own control column (ticket
	/// online-ui-paired-controls-and-page-scroll, S3): the game's own font measured on the row's label, or 0
	/// where this view carries none. An element whose game prefab could not be loaded has no label either,
	/// which is the same 0 — a column measured from what a placeholder drew would be a column measured from a
	/// fallback.
	/// </summary>
	internal float NameWidthOf(OnlineUiElementModel element)
	{
		if (element.Kind is OnlineUiElementKind.Label or OnlineUiElementKind.Button or OnlineUiElementKind.ColorSwatch
			|| _rowLabel is not { } label
			|| label.text.Length == 0)
		{
			return 0f;
		}

		return OnlineUiControlSizing.CaptionTextWidth(label);
	}

	/// <summary>Whether this element carries the name-plus-control pair the page's column aligns.</summary>
	internal bool HasNameAndControl => _rowLabel != null && _controlRect != null;
}
