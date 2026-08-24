namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// A remote player's projected screen position plus its stable SteamId. The
/// values are in GUI coordinates (y grows down), the same representation used
/// by <see cref="OffScreenArrowGeometry"/>. This is deliberately free of Unity
/// types so the overlap hit-test can be unit tested.
/// </summary>
public readonly struct RemoteScreenTarget(ulong steamId, float x, float y)
{
	public ulong SteamId { get; } = steamId;

	public float X { get; } = x;

	public float Y { get; } = y;
}
