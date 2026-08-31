namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>Screen-space rectangle for an IMGUI nameplate (origin top-left, y grows down).</summary>
public readonly record struct NameplateRect(float X, float Y, float Width, float Height);
