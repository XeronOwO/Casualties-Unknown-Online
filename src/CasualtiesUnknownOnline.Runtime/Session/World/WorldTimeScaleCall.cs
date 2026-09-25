using System;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// What a local <c>PlayerCamera.SetTimeScale</c> call IS, and what this side must
/// therefore do with it. The native method's own two flags already separate the
/// populations (reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:653):
/// <c>force</c> overrides the paused/death-screen guard and is passed only by the
/// pause/menu/death transitions (PauseHandler.cs:155/:159, PlayerCamera.cs:2293),
/// <c>switchSound</c> is the game's own "this change plays the speed sound"
/// marker and is true exactly for the deliberate speed changes (the speed
/// hotkeys :887-895, the console's AutoTimeScaleSet :649, the scripted
/// sequences :507/:562/:584), and a call with neither is a SILENT AUTOMATIC
/// RESET — the movement rule (:921-924), waking up (:2197), the scene start
/// (:734) and the in-world event resets (Vomiter.cs:52/:105,
/// SpiderHandler.cs:94/:269, SelfHarmer.cs:46, SurvivorNote.cs:72).
///
/// Only a deliberate change is a speed INTENT. The movement rule used to be
/// routed as one, so a single member's left/right tap ended the session-wide
/// acceleration for everyone (user report 2026-09-21, decision 223); a silent
/// reset is not a speed change the player asked for and the acceleration stands
/// through it. The reset is still the game's own local behaviour, so it must
/// not fight the session clock either: this screen keeps the session's speed —
/// see <see cref="ShouldRestoreSessionSpeed"/>.
///
/// <see cref="Route"/> is the decision table the Game Adapter executes verbatim,
/// so the rule that decides who may write the clock is pure and testable rather
/// than buried in the adapter's branches. Pure: no Unity, no clock, no session.
/// </summary>
public static class WorldTimeScaleCall
{
	/// <summary>Which of CUO's speed families a game speed belongs to.</summary>
	public enum SpeedFamily
	{
		/// <summary>Normal/Fast/SuperFast — the speeds the shared clock carries.</summary>
		SharedClock,

		/// <summary>UnconsciousFast/DyingFast — the vanilla per-side sleep fast-forward, owned by the host's all-unconscious policy.</summary>
		SleepOwned,

		/// <summary>Slowmo/Paused — local presentation, never on the wire.</summary>
		Presentation,
	}

	/// <summary>What the call is, from the session's point of view.</summary>
	public enum Kind
	{
		/// <summary>Slowmo/Paused — local presentation.</summary>
		LocalPresentation,

		/// <summary>The vanilla sleep fast-forward — suppressed on a guest, the host's own on the host.</summary>
		SleepOwned,

		/// <summary>A forced pause/menu/death transition (force:true): it overrides the paused/death-screen guard and stays local-only on a guest.</summary>
		Transition,

		/// <summary>An announced speed change (switchSound:true) — the only kind that owns the shared clock.</summary>
		Deliberate,

		/// <summary>A silent automatic reset (neither flag) — never owns the shared clock, and this screen keeps the session's speed through it.</summary>
		AutomaticReset,
	}

	/// <summary>What this side must do with the call.</summary>
	public enum Action
	{
		/// <summary>Run it: the host owns the clock and its postfix adopts the change as the standing request.</summary>
		RunAsAuthority,

		/// <summary>Run it locally and report the intent (a guest's announced change).</summary>
		RunLocalFirstAndReport,

		/// <summary>Run it locally, report nothing (forced transitions, Slowmo/Paused, the host's own sleep speed).</summary>
		RunLocalOnly,

		/// <summary>The start gate owns the clock: the call does not run and nothing is reported.</summary>
		DeferToStartGate,

		/// <summary>Not a speed intent and nothing to put back: the call does not run and nothing is reported.</summary>
		Swallow,

		/// <summary>A silent automatic reset inside a live session: the call does not run and this screen is kept on the session speed.</summary>
		SwallowAndKeepSessionSpeed,
	}

	/// <summary>
	/// The classification of one call. The SPEED FAMILY is read before either
	/// flag: a presentation speed stays a presentation effect (the survivor note's
	/// silent Slowmo), and the sleep speeds stay host-owned even though their calls
	/// carry neither flag — the native sites confirm that order is unobservable
	/// today (`force:true` never meets a sleep speed), and it is declared here so a
	/// future call site cannot reclassify them by accident. On a shared-clock speed
	/// a forced call is a transition (the guard it overrides is what makes it one)
	/// and only an announced one is deliberate. An unmapped family THROWS rather
	/// than falling through.
	/// </summary>
	public static Kind Classify(SpeedFamily family, bool switchSound, bool force) => family switch
	{
		SpeedFamily.Presentation => Kind.LocalPresentation,
		SpeedFamily.SleepOwned => Kind.SleepOwned,
		SpeedFamily.SharedClock => force
			? Kind.Transition
			: switchSound ? Kind.Deliberate : Kind.AutomaticReset,
		_ => throw new ArgumentOutOfRangeException(nameof(family), family, "a new speed family must be classified deliberately"),
	};

	/// <summary>
	/// The decision the Game Adapter executes. Outside a session the vanilla
	/// behaviour stands (the call runs locally and nothing is reported). Inside
	/// one: a silent automatic reset never writes the clock — and while the start
	/// gate holds the clock it writes nothing at all, so the gate's 0 survives —
	/// an announced change is the host's own or a guest's local-first report, the
	/// sleep fast-forward stays the host's, and forced transitions and Slowmo/
	/// Paused stay local on both sides whatever the gate says. An unmapped kind
	/// THROWS: falling through would route a call nobody classified.
	/// </summary>
	public static Action Route(Kind kind, bool isHost, bool sessionActive, bool atStartGate)
	{
		if (!sessionActive)
		{
			return Action.RunLocalOnly;
		}

		return kind switch
		{
			Kind.AutomaticReset => atStartGate ? Action.Swallow : Action.SwallowAndKeepSessionSpeed,
			Kind.Deliberate => isHost
				? Action.RunAsAuthority
				: atStartGate ? Action.DeferToStartGate : Action.RunLocalFirstAndReport,
			Kind.SleepOwned => isHost ? Action.RunLocalOnly : Action.Swallow,
			Kind.Transition or Kind.LocalPresentation => Action.RunLocalOnly,
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "a new call kind must be routed deliberately"),
		};
	}

	/// <summary>
	/// Whether a silent reset must be answered by putting this screen back on the
	/// session's speed. False while the local initiation owns the clock (its
	/// pending or ramping value is deliberately ahead of the host's) and false
	/// when the live clock already is the session speed — which is the movement
	/// case, so a movement key writes nothing at all: no dip, no speed sound.
	/// </summary>
	public static bool ShouldRestoreSessionSpeed(Kind kind, bool sessionClockInFlight, bool liveClockAtSessionSpeed) =>
		kind == Kind.AutomaticReset && !sessionClockInFlight && !liveClockAtSessionSpeed;
}
