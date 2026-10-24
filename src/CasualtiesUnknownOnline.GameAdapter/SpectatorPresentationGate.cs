using System;
using UnityEngine;

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

	/// <summary>
	/// The WATCHED body's own screen state, mirrored onto the spectator's camera: the black amount its consciousness
	/// would produce and the vignette colour. The service writes it every frame it follows a body that is on this
	/// client; the effects patch reads it instead of the local body, which is what makes the watched player's
	/// blackout and blood-loss vignette the thing the spectator sees (the ruling's "live it with them").
	/// </summary>
	internal static float WatchedBlackAmount { get; private set; }

	internal static Color WatchedVignette { get; private set; } = Color.clear;

	/// <summary>
	/// Non-zero while the spectator's own code is starting or stopping the WATCHED body's crisis sound: the sound
	/// patch refuses every clip while spectating (the local body's audio must not reach the watcher), and this lets
	/// the one sound the mode plays on the watched player's behalf through — a depth, not a flag, so a nested call
	/// cannot leave it open.
	/// </summary>
	internal static int WatchedSoundDepth { get; private set; }

	internal static bool AllowsWatchedSound => WatchedSoundDepth > 0;

	internal static void BeginWatchedSound() => WatchedSoundDepth++;

	internal static void EndWatchedSound() => WatchedSoundDepth = Math.Max(0, WatchedSoundDepth - 1);

	/// <summary>Counts the sounds the gate lets through while spectating (the observed players' replays and the one
	/// crisis sound), so a reading can prove the world is audible rather than silent.</summary>
	internal static int WorldSoundsHeard { get; private set; }

	internal static void NoteWorldSound() => WorldSoundsHeard++;

	/// <summary>
	/// The body the game's own HUD must present while the mode is on: the WATCHED player's, so the status icons and
	/// condition icons are theirs and not the watcher's — the ruling that spectating must look like playing, except
	/// the operator is someone else. Null means "present the local body", which is what the native managers were
	/// built with.
	/// </summary>
	internal static Body? HudBody { get; private set; }

	internal static void SetHudBody(Body watched) => HudBody = watched;

	internal static void ClearHudBody() => HudBody = null;

	/// <summary>True when the gate has a watched body the HUD may present.</summary>
	internal static bool HasHudBody => HudBody != null;

	internal static void ResetWorldSounds() => WorldSoundsHeard = 0;

	internal static void Set(bool active) => IsActive = active;

	internal static void SetWatched(float blackAmount, Color vignette)
	{
		WatchedBlackAmount = blackAmount;
		WatchedVignette = vignette;
	}

	internal static void ClearWatched() => SetWatched(0f, Color.clear);
}
