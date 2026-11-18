using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The game's own container feedback, played where CUO performs a container move itself.
///
/// <para>
/// Natively the clip comes from <c>PlayerCamera.PlayBackpackSound</c>: its drag/container branches call it when a move
/// lands inside a container (<c>PlayerCamera.cs:1748</c>, <c>:1828</c>, <c>:1855</c>), and <c>Body.PickUpItem</c> calls it
/// when an item leaves a container (<c>Body.cs:1467-1483</c>). A cross-player give into a container is the same move, but
/// CUO performs it in its own placement and container-intent code, so the native call never ran — measured 2026-11-19: a
/// give into the member's trash bag landed on the receiver's client with no clip on either side, while the same recorder
/// read the placement and break clips in the neighbouring rows.
/// </para>
///
/// <para>
/// Played once per gesture, on the client that performs the move; a body-slot arrival stays silent because the native
/// rule plays nothing there.
/// </para>
/// </summary>
internal static class ContainerMoveFeedback
{
	private static int _lastFrame = -1;

	/// <summary>Plays the game's container move clip on this client, once per frame.</summary>
	/// <remarks>
	/// Once per frame because a single gesture can reach two seams on the same client — the give runs the placement
	/// and then the container intent on the receiver — and natively one gesture plays one clip.
	/// </remarks>
	internal static void Play()
	{
		// A single gesture can reach both seams in one frame; Time.frameCount is the same frame for both calls.
		if (_lastFrame == Time.frameCount)
		{
			return;
		}

		var camera = PlayerCamera.main;
		if (camera == null) // Unity object — ==
		{
			return;
		}

		_lastFrame = Time.frameCount;
		camera.PlayBackpackSound();
	}
}
