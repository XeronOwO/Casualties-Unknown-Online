namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The drag-release rule for remote-clone inventory display proxies. A display
/// proxy is presentation-only; the only legitimate release outcome is the
/// remote-backpack take path consuming it. If that path did not consume it,
/// the drag must be cancelled before any native/local/cross-player release
/// logic can move the proxy into an authoritative body (the observed duplicate
/// water-bottle path: close the remote view while dragging, then drop the
/// still-held proxy into the local backpack).
/// </summary>
internal static class RemoteProxyDragPolicy
{
	internal static bool ShouldCancelProxyRelease(bool isRemoteProxy, bool remoteTakeHandled) =>
		isRemoteProxy && !remoteTakeHandled;
}
