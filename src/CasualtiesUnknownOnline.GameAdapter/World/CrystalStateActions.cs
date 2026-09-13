using CasualtiesUnknownOnline.Runtime.Session.World;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Shared crystal-family state actions (extracted from TrapStateActions at the
/// 600-line gate when the unstable-crystal ticking joined the family) — the
/// SAME application runs on the host (TrapEffectApplier) and on the replaying
/// guests (TrapVisualReplay): find the crystal at the event position, apply the
/// transition, the crystal's own Update/animation drives the rest. Each action
/// mirrors the game's own code path (the trigger side ran the original) and
/// returns <see cref="TrapActionOutcome"/>: APPLIED when it wrote the
/// transition, ALREADY IN STATE when the local copy had consumed it (a
/// duplicate event — the two-trigger race: both sides touched the same crystal
/// almost simultaneously), and NOT APPLICABLE when this copy cannot carry the
/// fact at all (the divergence case — a restore must not count such a row as
/// reached; see <see cref="TrapActionVerdict"/>).
/// </summary>
internal static class CrystalStateActions
{
	/// <summary>The internal effect types whose one-shot latch this library mirrors —
	/// found by runtime type name through <see cref="CrystalEffectAccess"/>, the same way
	/// the mimic and the unstable accessors find theirs.</summary>
	private const string EmpTypeName = "CrystalEMP";

	private const string MetamorphicTypeName = "CrystalMetamorphic";

	private const string ShyTypeName = "CrystalShy";

	/// <summary>Fragile crystal: consume the break — glass sound + health = 0 as a
	/// REMOTE death (the drops rolled on the triggering side). The position key
	/// already located the crystal (CrystalEffect is a plain class, not a
	/// component — the CrystalBehaviour carries the transform); the health
	/// check drops duplicates.</summary>
	internal static TrapActionOutcome ApplyCrystalFragile(CrystalBehaviour crystal)
	{
		if (crystal.build.health < 0.5f)
		{
			return TrapActionOutcome.AlreadyInState; // already consumed — a duplicate event
		}

		Sound.Play("glass", crystal.transform.position, false, true, null, 1f, 1f, false, false);
		crystal.build.health = 0f;
		crystal.gameObject.AddComponent<RemoteEntityDeath>();
		return TrapActionOutcome.Applied;
	}

	/// <summary>Electric crystal shock: zap + shake (the ring animation runs on
	/// the crystal's own Update everywhere).</summary>
	internal static TrapActionOutcome ApplyCrystalElectric(CrystalBehaviour crystal)
	{
		Sound.Play("zap", crystal.transform.position, false, true, null, 1f, 1f, false, false);
		PlayerCamera.main.shaker.Shake(200f);
		return TrapActionOutcome.Applied;
	}

	/// <summary>Teleport crystal touched (CrystalTeleport.cs:14-38): the
	/// triggering side's body teleports locally — position/consciousness/shock/
	/// velocity ride the 20 Hz player entity stream. The shared observable is
	/// the exact 2D observerlaugh + FlashBrief the trigger side played; this
	/// event is repeatable (no crystal latch) and does not write any entity
	/// state.</summary>
	internal static TrapActionOutcome ApplyCrystalTeleport(CrystalBehaviour crystal)
	{
		Sound.Play("observerlaugh", Vector2.zero, true, false, null, 1f, 1f, true, true);
		PlayerCamera.main.StartCoroutine("FlashBrief");
		return TrapActionOutcome.Applied;
	}

	/// <summary>Unstable crystal ticked (transient): THIS side's copy now replays
	/// the 5 s pre-explosion ticking the trigger side's StartTimer started
	/// (CrystalUnstable.cs:31-37) — the crystaltick sound + the CrystalTickingReplay
	/// component's glowing/jittering visual driven from this side's OWN clock.
	/// The private timerStarted/timer latches are NOT written: a written latch
	/// would make the local CrystalUnstable.Update count down and explode the
	/// crystal naturally, double-applying the world effects that the
	/// CrystalUnstableExploded event already replays (the mine-press rule). A
	/// copy already ticking natively (its local player touched it — the
	/// two-trigger race) or already replaying drops.</summary>
	internal static TrapActionOutcome ApplyCrystalUnstableTicked(CrystalBehaviour crystal)
	{
		if (CrystalUnstableAccess.IsTimerStarted(crystal) || CrystalTickingReplay.IsPresent(crystal))
		{
			return TrapActionOutcome.AlreadyInState; // already ticking natively / already replaying — a duplicate event
		}

		Sound.Play("crystaltick", crystal.transform.position, true, false, crystal.transform, 1f, 1f, false, false);
		CrystalTickingReplay.Begin(crystal);
		return TrapActionOutcome.Applied;
	}

	/// <summary>Mimic crystal triggered: consume the one-shot latch (the
	/// observerlaugh + crystalenemy spawns ran on the triggering side; the
	/// spawned enemies ride EntitySpawned + EnemyRuntimeSpawn, never here).
	/// Live replays play the SAME 2D observerlaugh call as the trigger side
	/// (CrystalMimic.cs:29/43); a late-joiner snapshot replay passes
	/// playSound=false — an old laugh must not fire over the joiner. The access
	/// helper's verdict is passed through UNCHANGED: "the latch was already
	/// consumed" and "this crystal carries no mimic effect" are different answers
	/// (see the class doc), and only the second is a row the restore must count as
	/// refused.</summary>
	internal static TrapActionOutcome ApplyCrystalMimic(CrystalBehaviour crystal, bool playSound)
	{
		var outcome = CrystalMimicAccess.TryActivate(crystal);
		if (outcome is not TrapActionOutcome.Applied)
		{
			return outcome; // already consumed (a duplicate) or no mimic on this copy (not applicable)
		}

		if (playSound)
		{
			Sound.Play("observerlaugh", Vector2.zero, true, false, null, 1f, 1f, true, true);
		}

		return TrapActionOutcome.Applied;
	}

	/// <summary>Metamorphic crystal triggered: the trigger side's path is the white
	/// screen flash + health = 0 + 1..4 drops + the laugh (CrystalMetamorphic.cs:16-35),
	/// and the DROPS are the trigger side's item-domain fact. This action mirrors the
	/// ENTITY half: the effect's `activated` latch (the one-shot mark its patch reports,
	/// `TrapCrystalPatch.MetamorphicTouchedPrefix/Postfix`), the white flash, and the
	/// health kill as a REMOTE death (no drop roll here: the drops the saved/triggering
	/// world already rolled must not be rolled again).
	///
	/// The LATCH is why the death alone is not enough: `health = 0` only makes the
	/// entity's own Update destroy it on its NEXT run (BuildingEntity.cs:56) and
	/// `RemoteEntityDeath` suppresses only the BUILDING's drop roll, not this effect's
	/// own — so in the frame between the replay and that Update the peer's own player
	/// could still touch the crystal, run the game's own `Touched`, and roll a second set
	/// of 1..4 drops, because the guard that method checks IS this latch
	/// (CrystalMetamorphic.cs:18-21/:27-33). The latch is therefore written FIRST, like
	/// the EMP and shy actions write theirs, which also makes the local-touch race end at
	/// the replay instead of at the next frame. A crystal already gone (health 0) is a
	/// duplicate; one carrying no metamorphic effect is NOT APPLICABLE (the effect list is
	/// rolled per crystal).
	///
	/// Unlike <see cref="ApplyCrystalFragile"/>, whose kind has no latch and rolls no
	/// drops of its own, this kind needs both marks: the latch for the effect's guard and
	/// the remote death for the building's drops.</summary>
	internal static TrapActionOutcome ApplyCrystalMetamorphic(CrystalBehaviour crystal)
	{
		if (crystal.build.health < 0.5f)
		{
			return TrapActionOutcome.AlreadyInState; // the crystal is already gone — a duplicate event
		}

		var outcome = CrystalEffectAccess.TryActivate(crystal, MetamorphicTypeName);
		if (outcome is not TrapActionOutcome.Applied)
		{
			return outcome; // already consumed (a duplicate) or no metamorphic effect on this copy (not applicable)
		}

		PlayerCamera.main.StartCoroutine("FlashBrief");
		Sound.Play("crystalenemylaugh", crystal.transform.position, true, false, null, 1f, 1f, false, false);
		crystal.build.health = 0f;
		crystal.gameObject.AddComponent<RemoteEntityDeath>();
		return TrapActionOutcome.Applied;
	}

	/// <summary>Shy crystal swapped: mirror the trigger side's path — the effect's
	/// `activated` latch, then the scan for the first other crystal within 64 units
	/// (CrystalShy.cs:17-30) and the swap, the observerlaugh the swap's audible cue.
	/// The scan order has no formal guarantee, but the crystals are generation-static
	/// and the world is deterministic (recorded in the entity-features matrix).
	///
	/// The LATCH is written FIRST, because the trigger patch observes exactly that rise
	/// (TrapCrystalPatch.ShyTouchedPrefix/Postfix → ReportCrystal) and the game writes it
	/// after the scan whatever the scan found (CrystalShy.cs:31): a replay that skipped it
	/// left this copy ARMED — its own player's next touch swapped again and reported a
	/// second row — and answered APPLIED for a state it had never written. The latch is
	/// mirrored through <see cref="CrystalEffectAccess"/> with the mimic's tri-state: a
	/// duplicate answers ALREADY IN STATE, a crystal carrying no shy effect is NOT
	/// APPLICABLE.
	///
	/// The VERDICT answers the latch — the state the trigger patch reports
	/// (`TrapCrystalPatch.ShyTouchedPrefix/Postfix`) — so a freshly written latch is
	/// APPLIED whatever the scan found: a scan that finds no partner is the same no-op the
	/// GAME's own `Touched` produces on that copy (it also just writes the latch and
	/// leaves the positions alone), so it is a faithful mirror, not a missing fact. The
	/// scan itself stays a faithful mirror — the game's own scan can match the crystal's
	/// own collider, and that is a no-op swap on BOTH sides.
	///
	/// The row's POSITION is the one the report patch reads AFTER the swap
	/// (TrapCrystalPatch.ShyTouchedPostfix → ReportCrystal →
	/// `crystal.crystal.transform.position`), i.e. the partner's pre-swap position
	/// unless the scan matched the crystal itself. A replay therefore re-runs the scan
	/// from there — on a fresh world, on whichever crystal stands at that position —
	/// and its pairing is decided by the same unspecified `Physics2D.OverlapCircleAll`
	/// order the game itself carries. Recorded as it stands rather than changed: the
	/// mirror is faithful, and whether the pair re-swaps can only be settled by the
	/// dual-client pass, not by a test host without a physics engine.</summary>
	internal static TrapActionOutcome ApplyCrystalShy(CrystalBehaviour crystal)
	{
		var outcome = CrystalEffectAccess.TryActivate(crystal, ShyTypeName);
		if (outcome is not TrapActionOutcome.Applied)
		{
			return outcome; // already consumed (a duplicate) or no shy effect on this copy (not applicable)
		}

		foreach (var collider in Physics2D.OverlapCircleAll(crystal.transform.position, 64f, LayerMask.GetMask("Ground")))
		{
			if (collider.GetComponent<CrystalBehaviour>() != null) // Unity object — ==
			{
				var target = collider.transform;
				var self = crystal.transform;
				var targetPos = target.position;
				var targetRot = target.rotation;
				var selfPos = self.position;
				var selfRot = self.rotation;
				target.SetPositionAndRotation(selfPos, selfRot);
				self.SetPositionAndRotation(targetPos, targetRot);
				Sound.Play("observerlaugh", self.position, true, false, null, 1f, 1f, false, false);
				break;
			}
		}

		return TrapActionOutcome.Applied;
	}

	/// <summary>EMP crystal activated: the battery drain rides the item domain (the
	/// trigger side drained ITS OWN player's batteries — CrystalEMP.cs:21-31); this
	/// action writes the state the row names — the effect's `activated` latch — and the
	/// observables: the white flash + the crystalemp sound + the shake
	/// (CrystalEMP.cs:32-34).
	///
	/// The LATCH is not optional: the crystal's own Update returns unless it is set
	/// (CrystalEMP.cs:54-58) and only then lerps the sprite to black, so a replay that
	/// skipped it left the crystal permanently WHITE, and — worse — still armed, so this
	/// side's own player could touch it and drain their own batteries. The latch is
	/// mirrored through <see cref="CrystalEffectAccess"/> with the same tri-state the
	/// mimic uses: a duplicate answers ALREADY IN STATE and a crystal carrying no EMP
	/// effect is NOT APPLICABLE (the effect list is rolled per crystal).</summary>
	internal static TrapActionOutcome ApplyCrystalEMP(CrystalBehaviour crystal)
	{
		var outcome = CrystalEffectAccess.TryActivate(crystal, EmpTypeName);
		if (outcome is not TrapActionOutcome.Applied)
		{
			return outcome; // already consumed (a duplicate) or no EMP on this copy (not applicable)
		}

		crystal.SetColor(Color.white);
		Sound.Play("crystalemp", crystal.transform.position, true, false, null, 1f, 1f, false, false);
		PlayerCamera.main.shaker.Shake(200f);
		return TrapActionOutcome.Applied;
	}
}
