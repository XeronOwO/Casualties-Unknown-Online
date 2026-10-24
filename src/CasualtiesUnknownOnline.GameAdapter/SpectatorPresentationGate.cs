namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The spectator mode's presentation gate. The ruling is that watching is PURE observation: the local
/// character's own presentation — its sounds, its screen effects, the vital-crisis heartbeat and the
/// normal-play input — must not reach a player who is spectating.
///
/// <para>
/// The mode itself lives in <see cref="World.SpectatorCameraService"/>; this static is the one bit the
/// static Harmony patches can read (they cannot be handed anything by construction, and a whole bridge
/// port for a boolean would be a door for one flag). It is written only on entering and leaving the mode
/// and on a session reset, and it reads as "not spectating" until then, so a patch that runs before the
/// mode exists keeps the game's own behaviour.
/// </para>
/// </summary>
internal static class SpectatorPresentationGate
{
	/// <summary>True while the local client is in spectator mode: the player-state presentation is off.</summary>
	internal static bool IsActive { get; private set; }

	internal static void Set(bool active) => IsActive = active;
}
