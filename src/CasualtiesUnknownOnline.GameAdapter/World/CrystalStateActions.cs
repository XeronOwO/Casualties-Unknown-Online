using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Shared crystal-family state actions (extracted from TrapStateActions at the
/// 600-line gate when the unstable-crystal ticking joined the family) — the
/// SAME application runs on the host (TrapEffectApplier) and on the replaying
/// guests (TrapVisualReplay): find the crystal at the event position, apply the
/// transition, the crystal's own Update/animation drives the rest. Each action
/// mirrors the game's own code path (the trigger side ran the original) and
/// returns whether it APPLIED — false means the local copy already consumed the
/// transition (a duplicate event — the two-trigger race: both sides touched
/// the same crystal almost simultaneously). The caller logs the drop.
/// </summary>
internal static class CrystalStateActions
{
	/// <summary>Fragile crystal: consume the break — glass sound + health = 0 as a
	/// REMOTE death (the drops rolled on the triggering side). The position key
	/// already located the crystal (CrystalEffect is a plain class, not a
	/// component — the CrystalBehaviour carries the transform); the health
	/// check drops duplicates.</summary>
	internal static bool ApplyCrystalFragile(CrystalBehaviour crystal)
	{
		if (crystal.build.health < 0.5f)
		{
			return false; // already consumed — a duplicate event
		}

		Sound.Play("glass", crystal.transform.position, false, true, null, 1f, 1f, false, false);
		crystal.build.health = 0f;
		crystal.gameObject.AddComponent<RemoteEntityDeath>();
		return true;
	}

	/// <summary>Electric crystal shock: zap + shake (the ring animation runs on
	/// the crystal's own Update everywhere).</summary>
	internal static bool ApplyCrystalElectric(CrystalBehaviour crystal)
	{
		Sound.Play("zap", crystal.transform.position, false, true, null, 1f, 1f, false, false);
		PlayerCamera.main.shaker.Shake(200f);
		return true;
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
	internal static bool ApplyCrystalUnstableTicked(CrystalBehaviour crystal)
	{
		if (CrystalUnstableAccess.IsTimerStarted(crystal) || CrystalTickingReplay.IsPresent(crystal))
		{
			return false; // already ticking natively / already replaying — a duplicate event
		}

		Sound.Play("crystaltick", crystal.transform.position, true, false, crystal.transform, 1f, 1f, false, false);
		CrystalTickingReplay.Begin(crystal);
		return true;
	}

	/// <summary>Mimic crystal triggered: consume the one-shot latch (the
	/// observerlaugh + crystalenemy spawns ran on the triggering side; the
	/// spawned enemies ride EntitySpawned + EnemyRuntimeSpawn, never here).
	/// Live replays play the SAME 2D observerlaugh call as the trigger side
	/// (CrystalMimic.cs:29/43); a late-joiner snapshot replay passes
	/// playSound=false — an old laugh must not fire over the joiner.</summary>
	internal static bool ApplyCrystalMimic(CrystalBehaviour crystal, bool playSound)
	{
		if (!CrystalMimicAccess.TryActivate(crystal))
		{
			return false; // already consumed, or no mimic at this position
		}

		if (playSound)
		{
			Sound.Play("observerlaugh", Vector2.zero, true, false, null, 1f, 1f, true, true);
		}

		return true;
	}

	/// <summary>Metamorphic crystal triggered (the death rides BuildingEntityDamaged,
	/// the drops ride the item domain — this syncs the remaining observables):
	/// the white screen flash + the laugh, exactly the trigger side's path
	/// (CrystalMetamorphic.cs:25, :32). Re-applying is harmless — the death
	/// consumption is what guards duplicates.</summary>
	internal static bool ApplyCrystalMetamorphic(CrystalBehaviour crystal)
	{
		PlayerCamera.main.StartCoroutine("FlashBrief");
		Sound.Play("crystalenemylaugh", crystal.transform.position, false, true, null, 1f, 1f, false, false);
		return true;
	}

	/// <summary>Shy crystal swapped: re-run the trigger side's scan — the first
	/// other crystal within 64 units (CrystalShy.cs:17-30) — and swap the
	/// positions, the observerlaugh the swap's audible cue. The scan order has
	/// no formal guarantee, but the crystals are generation-static and the world
	/// is deterministic (recorded in the entity-features matrix).</summary>
	internal static bool ApplyCrystalShy(CrystalBehaviour crystal)
	{
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
				Sound.Play("observerlaugh", self.position, false, true, null, 1f, 1f, false, false);
				break;
			}
		}

		return true;
	}

	/// <summary>EMP crystal activated (the battery drain rides the item domain):
	/// the white flash + the crystalemp sound + the shake — the darkening runs on
	/// the crystal's own Update once it is white (CrystalEMP.cs:54-64), so the
	/// black-state transition is what the update drives afterwards.</summary>
	internal static bool ApplyCrystalEMP(CrystalBehaviour crystal)
	{
		crystal.SetColor(Color.white);
		Sound.Play("crystalemp", crystal.transform.position, false, true, null, 1f, 1f, false, false);
		PlayerCamera.main.shaker.Shake(200f);
		return true;
	}
}
