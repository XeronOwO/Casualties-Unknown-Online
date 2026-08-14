namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The local player's swing-presentation window (PURE — no Unity, time is an
/// explicit input): <c>Body.Attack</c> (Body.cs:1887) and <c>Body.ThrowItem</c>
/// (Body.cs:1665) both play the one-shot <c>ArmsSwing</c> clip once per swing,
/// and the peer's render clone must replay it. The swing is therefore held as
/// the <see cref="PlayerEntity.IsAttacking"/> flag for the clip's visible span
/// so the peer's clone can edge-detect the rising flag and play the clip once.
/// The hold covers BOTH the semantic "swing in progress" AND the unreliable
/// 20 Hz state stream (one snapshot must carry the rising edge — six ticks at
/// the window below). The GameAdapter's <c>Body.Attack</c>/<c>ThrowItem</c>
/// patches report the swing fact; <see cref="EntitySyncService"/> feeds this
/// machine the tick and publishes <see cref="IsAttacking"/> into the snapshot.
/// </summary>
internal sealed class AttackSwingState
{
	/// <summary>
	/// The <c>ArmsSwing</c> clip's visible span (ms). Evidence: the attackRot
	/// procedural lean decays to zero at 3/s (Body.cs:3354, a ~0.3 s swing) and
	/// a 300 ms hold covers six 20 Hz ticks — enough for the unreliable stream
	/// to deliver the rising edge even with a few dropped snapshots.
	/// </summary>
	internal const long SwingDurationMs = 300;

	private long _swingStartedMs = long.MinValue;

	/// <summary>True while the swing window holds — the peer's clone replays the ArmsSwing clip on its rising edge.</summary>
	internal bool IsAttacking { get; private set; }

	/// <summary>A swing ran (Body.Attack — conscious + off-cooldown + doAttackAnim, or Body.ThrowItem with an item) — start (or restart) the window.</summary>
	internal void MarkAttack(long nowMs)
	{
		_swingStartedMs = nowMs;
		IsAttacking = true;
	}

	/// <summary>Advance the window: the swing ends once its visible span elapsed.</summary>
	internal void Tick(long nowMs)
	{
		if (IsAttacking && nowMs - _swingStartedMs >= SwingDurationMs)
		{
			IsAttacking = false;
		}
	}

	/// <summary>The world/session ended — a swing cannot outlive its world (a stale flag would re-trigger on the next world entry).</summary>
	internal void Reset()
	{
		_swingStartedMs = long.MinValue;
		IsAttacking = false;
	}
}
