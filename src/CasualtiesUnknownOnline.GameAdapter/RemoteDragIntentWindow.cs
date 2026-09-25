using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The one live release window of this client. The state machine itself lives in
/// the runtime (it holds no game types and is covered without a scene); the
/// patches reach it through this holder, which keeps a single window per process
/// — one local client can only have one drag release in flight.
/// </summary>
internal static class RemoteDragIntentWindow
{
	internal static RemoteDragIntentCapture Current { get; } = new();
}
