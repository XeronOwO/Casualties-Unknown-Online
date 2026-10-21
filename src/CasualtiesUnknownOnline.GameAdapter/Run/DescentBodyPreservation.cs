using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The one handback a descent follow must refuse: the saved-character restore the host
/// sends when the member reports InWorld at the end of its own layer generation. The
/// body came through the regeneration (the member never reloaded the scene), so that
/// restore would move it to its old layer's position and wipe/reapply the items it
/// already holds.
/// <para>
/// It is a leaf shared by <see cref="RunCoordinator"/> (which notes the body at the
/// descent's edge) and <c>CharacterDataSync</c> (which sees the restore arrive) — the
/// fact belongs to neither alone and a delegate wired after construction would be the
/// same fact with a temporal-coupling trap. The suppression is ONE-SHOT: only the
/// handback that answers that one InWorld edge is refused, and a revive handback (the
/// host's respawn sets <see cref="CharacterDataMsg.Position"/> to null on purpose) is
/// never refused — it is a real handback to the same body. The preserved body's own
/// identity bounds it: the host sends a saved character only on an InWorld edge, and a
/// body that actually left is a different object (or the note is cleared with it).
/// </para>
/// </summary>
internal sealed class DescentBodyPreservation
{
	/// <summary>The instance id of the local body that stood in the world when the descent was accepted; 0 = no follow.</summary>
	private int _bodyId;

	/// <summary>A descent was accepted on this body — the next saved-character handback for it is the descent's own.</summary>
	public void Note(int bodyId) => _bodyId = bodyId;

	/// <summary>The body is gone or the session ended — a later handback is an ordinary one.</summary>
	public void Clear() => _bodyId = 0;

	/// <summary>
	/// True exactly once, for the handback that answers the descent's InWorld edge on the preserved
	/// body. A handback for another body, a revive handback (no position), or anything after the first
	/// refused one returns false and is applied normally.
	/// </summary>
	public bool TryConsume(int localBodyId, CharacterDataMsg handback)
	{
		if (_bodyId == 0 || localBodyId == 0 || localBodyId != _bodyId)
		{
			return false;
		}

		if (handback.Position is null)
		{
			// The revive/respawn handback lands at the current world's spawn point by design: it is a
			// handback this body DOES need, not the "put the leaver back" snapshot.
			return false;
		}

		_bodyId = 0;
		return true;
	}
}
