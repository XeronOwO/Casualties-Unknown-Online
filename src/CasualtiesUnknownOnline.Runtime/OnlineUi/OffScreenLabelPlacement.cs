namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>Where one off-screen marker's label goes, and whether it had to be pinned because no side had room to hold it.</summary>
public readonly record struct OffScreenLabelPlacement(float X, float Y, bool Pinned);
