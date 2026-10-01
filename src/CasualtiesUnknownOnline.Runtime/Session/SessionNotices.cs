namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Default <see cref="ISessionNotices"/>: one pending slot, latest wins. Main
/// thread only — the watchdogs publish from the frame tick and the Online UI
/// drains from its own update, both on the Unity main thread, so no lock is
/// taken and none is needed.
/// </summary>
public sealed class SessionNotices : ISessionNotices
{
	private SessionNotice? _pending;

	public void Publish(string text) => _pending = new SessionNotice(text);

	public bool TryTake(out SessionNotice notice)
	{
		if (_pending is { } pending)
		{
			_pending = null;
			notice = pending;
			return true;
		}

		notice = default;
		return false;
	}
}
