namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One queued surface intent (<see cref="OnlineUiIntentKind"/>). A plain value, like every other value
/// that crosses the adapter boundary: the surface is a Unity object graph, and no part of it travels
/// back — only the fact that something happened, and which control it happened on.
///
/// <para>
/// The payload fields are one family with one meaning each: <see cref="ControlId"/> names the element
/// from the frame the player acted on (the same id the plugin registered that control's action under),
/// and the value fields carry what the control now holds — <see cref="Text"/> for a typed field,
/// <see cref="Number"/> for a slider, <see cref="Index"/> for a dropdown option, <see cref="Flag"/> for
/// a toggle. A kind that has no payload leaves them at their defaults.
/// </para>
/// </summary>
public readonly record struct OnlineUiIntent(
	OnlineUiIntentKind Kind,
	string ControlId = "",
	string Text = "",
	float Number = 0f,
	int Index = -1,
	bool Flag = false);
