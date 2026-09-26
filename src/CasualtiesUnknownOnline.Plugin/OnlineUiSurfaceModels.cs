using CasualtiesUnknownOnline.Runtime.OnlineUi;

namespace CasualtiesUnknownOnline;

/// <summary>
/// One frame's three CUO surfaces, built together from ONE action table (ticket
/// online-ui-art-and-controls-overhaul, S5). It exists so the window and the two panels cannot be built
/// from different frames' registrations: the table is cleared once, all three write into it, and the models
/// that travel to the surface are the ones those registrations belong to. Null means "that surface is not
/// shown" for each of them.
/// </summary>
internal readonly record struct OnlineUiSurfaceModels(
	OnlineUiWindowModel? Window,
	OnlineUiPanelModel? QuickPanel,
	OnlineUiPanelModel? ContextMenu);
