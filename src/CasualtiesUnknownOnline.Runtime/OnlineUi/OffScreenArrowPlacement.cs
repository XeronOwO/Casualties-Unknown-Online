namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// A marker placement in GUI coordinates (origin top-left, y grows down).
/// When <see cref="OffScreenArrowDirection"/> is not
/// <see cref="OffScreenArrowDirection.None"/>, X/Y are the clamped point on the
/// screen-edge rectangle where the arrow is drawn.
/// </summary>
public readonly record struct OffScreenArrowPlacement(float X, float Y, OffScreenArrowDirection Direction);
