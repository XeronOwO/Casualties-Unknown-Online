namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The one-slot notice channel between the session-liveness watchdogs and the
/// Online UI's status line. It exists because the watchdogs act inside the
/// session while the status line belongs to the plugin: a rendered line travels
/// (the runtime resolves its own catalogue), and a newer notice replaces an
/// unread one — the status line shows the latest event, not a backlog.
/// </summary>
public interface ISessionNotices
{
	/// <summary>Publishes one line for the status line; the latest one wins.</summary>
	void Publish(string text);

	/// <summary>Takes the pending notice, clearing the slot; false when there is none.</summary>
	bool TryTake(out SessionNotice notice);
}
