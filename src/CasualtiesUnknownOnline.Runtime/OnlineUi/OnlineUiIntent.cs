namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One queued surface intent (<see cref="OnlineUiIntentKind"/>). A plain value, like every other value
/// that crosses the adapter boundary: the surface is a Unity object graph, and no part of it travels
/// back — only the fact that something happened, and which control it happened on.
/// </summary>
public readonly record struct OnlineUiIntent(OnlineUiIntentKind Kind);
