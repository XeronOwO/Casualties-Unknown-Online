using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Marks the body <see cref="RemoteMedicalCoordinator"/> built as the remote
/// medical view's display-only copy — a body CUO renders and never simulates.
///
/// <para>
/// It exists because the limb-component codec's display half has to know WHAT
/// it is writing to. That half promises "state, never native behaviour", and the
/// promise holds only for a body CUO parked itself: the same call handed a LIVE
/// body (a render clone, a player's own body) instantiates the game's own
/// components exactly where the game's own <c>Start</c> and <c>Update</c> do
/// run. Batch <c>20261011-b</c> reached that state through the acceptance
/// driver, which pointed the focus at the peer's live render clone: the game's
/// <c>SplintLimb.Start</c> registered a wound-view icon, the renderer then
/// replaced the clone, and the icon's removal predicate dereferenced a destroyed
/// limb — a <c>NullReferenceException</c> every frame for as long as the panel
/// stayed open.
/// </para>
///
/// <para>
/// The read walks the limb's parents with <c>includeInactive: true</c>, so it answers on the parked
/// (inactive) hierarchy and does not depend on the copy sitting at a scene root: what is being asked is
/// "does this limb belong to a body CUO built", and any other answer is a refusal.
/// </para>
/// </summary>
internal sealed class RemoteMedicalDisplayBody : MonoBehaviour
{
	/// <summary>Whether this limb belongs to a display-only copy CUO created — never a live body.</summary>
	internal static bool IsDisplayCopy(Limb limb) =>
		limb != null && limb.GetComponentInParent<RemoteMedicalDisplayBody>(true) != null;
}
