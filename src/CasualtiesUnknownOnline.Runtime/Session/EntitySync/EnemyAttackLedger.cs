using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Victim-side attack identity ledger: an announced enemy attack carries the
/// host's per-enemy monotonic sequence, and the client that judges it applies at
/// most ONE attack per identity. A repeated announcement, a reordered one and a
/// stale one (an older sequence than one already judged) are all dropped, which
/// is what makes "one attack applies at most once" independent of the transport.
///
/// The identity of the HIGHEST JUDGED attack is recorded whatever the judgment
/// decided: an attack judged as a miss must not be re-judged and applied later
/// by a duplicate. The enemy id carries the world/layer epoch, so an id
/// re-allocated in a later generation starts a fresh entry. The ledger is never
/// evicted and does not need to be: an id can only be reused across sessions (the
/// epoch is per-session and the id counter never resets inside one), and a new
/// session starts with a fresh ledger.
/// </summary>
public sealed class EnemyAttackLedger
{
	private readonly Dictionary<NetworkEntityId, uint> _lastJudged = [];

	/// <summary>
	/// True when this announcement is new for its enemy and must be judged by the
	/// local client; false for a repeat, a reorder, and for a message carrying no
	/// identity (sequence 0 — the host always stamps one, so 0 is malformed and
	/// fails closed).
	/// </summary>
	public bool ShouldJudge(NetworkEntityId enemyId, uint attackSeq)
	{
		if (attackSeq == 0)
		{
			return false;
		}

		if (_lastJudged.TryGetValue(enemyId, out var lastJudged) && attackSeq <= lastJudged)
		{
			return false;
		}

		_lastJudged[enemyId] = attackSeq;
		return true;
	}
}
