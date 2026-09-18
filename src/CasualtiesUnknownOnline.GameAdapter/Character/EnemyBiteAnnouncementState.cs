using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Host-side per-spider announcement latch: true while the CURRENT bite action has
/// already been announced. The action is the game's own gate — the bite cooldown
/// is open, the spider is not stunned, and a player is inside the bite range
/// (<c>EnemyCombatPolicy.SpiderBiteRange</c>) — so the announcement fires once per
/// action and the latch clears the moment that gate closes, which is when the
/// spider's own cooldown/stun or an empty range starts the next action.
///
/// It lives on the spider rather than in a director-side table because a Unity
/// instance id can be reused after the object is destroyed, and a stale entry
/// would then swallow a LATER spider's first announcement.
/// </summary>
internal sealed class EnemyBiteAnnouncementState : MonoBehaviour
{
	/// <summary>True once the current bite action has been announced; cleared when the action ends.</summary>
	internal bool Announced { get; set; }
}
