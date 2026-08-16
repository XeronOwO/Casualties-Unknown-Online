using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// One in-world player's inputs to the world-time policy. StateKnown false
/// means CUO has no authoritative snapshot yet (a just-joined member) — the
/// policy then treats the player as moving, so a request can never accelerate
/// the world over an unobserved player.
/// </summary>
public readonly record struct WorldTimePlayerState(
	bool StateKnown,
	bool Alive,
	float Consciousness,
	bool BrainDying,
	float VelocityX,
	float VelocityY);

/// <summary>
/// The policy verdict: the speed the session must run at now, plus the
/// request value to KEEP. Movement and sleep acceleration clear the request —
/// a fast-forward must never re-apply itself after the blocking condition
/// ends.
/// </summary>
public readonly record struct WorldTimeDecision(WorldTimeSpeed Speed, WorldTimeSpeed NextRequested);

/// <summary>
/// Pure world-time policy (no Unity, no clock): the host feeds per-player
/// state and the current request, this returns the authoritative speed.
/// Priority: movement (Normal) > all-unconscious sleep acceleration >
/// requested speed. The game's own black-screen acceleration triggers below
/// 20 consciousness (PlayerCamera.cs:2220) and picks 3.5× while brain-dying,
/// otherwise 25× — the session uses the same thresholds and the slowest fair
/// speed (any dying player ⇒ 3.5×).
/// </summary>
public static class WorldTimePolicy
{
	/// <summary>Below this consciousness the game's black-screen fast-forward can start (PlayerCamera.HandleUnconsciousScreen).</summary>
	public const float SleepConsciousnessThreshold = 20f;

	/// <summary>Squared velocity threshold: a moving player overrides any fast speed. Velocity is a body velocity, so small physics jitter is ignored.</summary>
	public const float MovingSpeedSquaredThreshold = 0.25f;

	/// <summary>Guests may only request the three manual speeds — sleep speeds are host-computed, Slowmo/Paused are local-only.</summary>
	public static bool IsGuestRequestSpeed(WorldTimeSpeed speed) =>
		speed is WorldTimeSpeed.Normal or WorldTimeSpeed.Fast or WorldTimeSpeed.SuperFast;

	/// <summary>True while this player blocks fast-forward: unobserved, or alive + conscious + actually moving.</summary>
	public static bool IsMoving(in WorldTimePlayerState player)
	{
		if (!player.StateKnown)
		{
			return true;
		}

		if (!player.Alive || player.Consciousness <= SleepConsciousnessThreshold)
		{
			return false;
		}

		var velocitySquared = player.VelocityX * player.VelocityX + player.VelocityY * player.VelocityY;
		return velocitySquared > MovingSpeedSquaredThreshold;
	}

	/// <summary>
	/// The all-unconscious sleep speed: Normal when anyone alive is awake, when
	/// nobody alive remains, or when any in-world player state is unknown;
	/// DyingFast when any sleeping player is brain-dying; otherwise
	/// UnconsciousFast. Dead players are ignored (the death screen does not
	/// auto-accelerate in the base game).
	/// </summary>
	public static WorldTimeSpeed DecideSleepSpeed(IReadOnlyList<WorldTimePlayerState> players)
	{
		var anyAlive = false;
		var anyDying = false;
		foreach (var player in players)
		{
			if (!player.StateKnown)
			{
				return WorldTimeSpeed.Normal; // never accelerate over an unobserved player
			}

			if (!player.Alive)
			{
				continue;
			}

			anyAlive = true;
			if (player.Consciousness > SleepConsciousnessThreshold)
			{
				return WorldTimeSpeed.Normal; // someone is awake — no session-wide sleep
			}

			if (player.BrainDying)
			{
				anyDying = true;
			}
		}

		if (!anyAlive)
		{
			return WorldTimeSpeed.Normal;
		}

		return anyDying ? WorldTimeSpeed.DyingFast : WorldTimeSpeed.UnconsciousFast;
	}

	/// <summary>
	/// Decides the session speed and the request value to keep. Any moving
	/// player wins first (Normal + request cleared); otherwise the sleep policy
	/// wins (its speed + request cleared); otherwise the request stands.
	/// </summary>
	public static WorldTimeDecision Decide(WorldTimeSpeed requested, IReadOnlyList<WorldTimePlayerState> players)
	{
		foreach (var player in players)
		{
			if (IsMoving(player))
			{
				return new WorldTimeDecision(WorldTimeSpeed.Normal, WorldTimeSpeed.Normal);
			}
		}

		var sleepSpeed = DecideSleepSpeed(players);
		if (sleepSpeed != WorldTimeSpeed.Normal)
		{
			return new WorldTimeDecision(sleepSpeed, WorldTimeSpeed.Normal);
		}

		return new WorldTimeDecision(requested, requested);
	}
}
