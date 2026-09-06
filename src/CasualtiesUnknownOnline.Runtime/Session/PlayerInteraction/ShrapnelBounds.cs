using System;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The shared shrapnel minigame-space coordinate bounds. Both the session
/// arbitration service and the state writer use the same native clamp values.
/// </summary>
internal static class ShrapnelBounds
{
	internal static float ClampX(float x) => Math.Max(-524f, Math.Min(524f, x));

	internal static float ClampY(float y) => Math.Max(-364f, Math.Min(524f, y));
}
