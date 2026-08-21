using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Marks a LOCAL body as being carried by another player. While this component
/// is present the body's own per-frame simulation is skipped (BodyPatches
/// treats it like a render proxy) and GameAdapter drives its transform from the
/// carrier's entity state each frame, so the carried player's own client moves
/// its body to the carrier's back and reports that position through the normal
/// 20 Hz/1 Hz streams — all other peers see the carried body through ordinary
/// entity state, without a separate carry-specific render network.
/// </summary>
internal sealed class CarriedBodyDriver : MonoBehaviour
{
	/// <summary>The SteamId of the player carrying this body.</summary>
	public ulong CarrierSteamId;
}
