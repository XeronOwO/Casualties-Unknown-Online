using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// One in-world player's inputs to the world-time SLEEP policy. StateKnown
/// false means CUO has no authoritative snapshot yet (a just-joined member) —
/// the sleep policy then refuses to accelerate over a player it cannot observe.
/// Movement is deliberately NOT an input: a movement key is an ACTION of the
/// player who pressed it, taken on that player's own client (the native rule,
/// PlayerCamera.cs:921-924), never a velocity the host polls — user ruling
/// 2026-09-18, decision 184.
/// </summary>
public readonly record struct WorldTimePlayerState(
	bool StateKnown,
	bool Alive,
	float Consciousness,
	bool BrainDying);

/// <summary>
/// The policy verdict: the speed the session must run at now, plus the request
/// value to KEEP. The sleep branch clears the request — a sleep fast-forward
/// must never re-apply itself after everyone wakes.
/// </summary>
public readonly record struct WorldTimeDecision(WorldTimeSpeed Speed, WorldTimeSpeed NextRequested);

/// <summary>
/// Pure world-time policy (no Unity, no clock): the host feeds per-player state
/// and the current request, this returns the authoritative speed. Priority:
/// all-unconscious sleep acceleration (its speed, request cleared) > the
/// requested speed > Normal.
/// The game's own black-screen acceleration triggers below 20 consciousness
/// (PlayerCamera.cs:2217) and picks 3.5× while brain-dying, otherwise 25× —
/// the session uses the same thresholds and the slowest fair speed (any dying
/// player ⇒ 3.5×).
/// A manual Fast/SuperFast request is HONORED while the group is awake: the
/// initiator applies it locally at once and the host accepts first (user ruling
/// 2026-09-18, `review/world-time-local-initiation.md`) — only an invalid request
/// is refused, and a refusal never reaches this method.
/// </summary>
public static class WorldTimePolicy
{
	/// <summary>Below this consciousness the game's black-screen fast-forward can start (PlayerCamera.HandleUnconsciousScreen).</summary>
	public const float SleepConsciousnessThreshold = 20f;

	/// <summary>Guests may only request the three manual speeds — sleep speeds are host-computed, Slowmo/Paused are local-only.</summary>
	public static bool IsGuestRequestSpeed(WorldTimeSpeed speed) =>
		speed is WorldTimeSpeed.Normal or WorldTimeSpeed.Fast or WorldTimeSpeed.SuperFast;

	/// <summary>The five speeds CUO synchronizes; anything else a peer names is not a world-time speed.</summary>
	public static bool IsSynchronizedSpeed(WorldTimeSpeed speed) =>
		speed is WorldTimeSpeed.Normal or WorldTimeSpeed.Fast or WorldTimeSpeed.SuperFast
			or WorldTimeSpeed.UnconsciousFast or WorldTimeSpeed.DyingFast;

	/// <summary>An unknown value is not a world-time speed, so Normal is the only safe stand-in (used on both the request and the receive path).</summary>
	public static WorldTimeSpeed NormalizeSpeed(WorldTimeSpeed speed) =>
		IsSynchronizedSpeed(speed) ? speed : WorldTimeSpeed.Normal;

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
	/// Decides the session speed and the request value to keep. The
	/// all-unconscious sleep branch owns the clock while it applies (its speed,
	/// request cleared); otherwise the request stands — a manual acceleration is
	/// no longer discarded for an awake group, and an unrepresentable value
	/// degrades to Normal instead of reaching the wire.
	/// </summary>
	public static WorldTimeDecision Decide(WorldTimeSpeed requested, IReadOnlyList<WorldTimePlayerState> players)
	{
		var sleepSpeed = DecideSleepSpeed(players);
		if (sleepSpeed != WorldTimeSpeed.Normal)
		{
			return new WorldTimeDecision(sleepSpeed, WorldTimeSpeed.Normal);
		}

		var standing = NormalizeSpeed(requested);
		return new WorldTimeDecision(standing, standing);
	}
}
