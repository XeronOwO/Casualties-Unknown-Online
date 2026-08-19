using System.Collections;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Shared state-family actions for the lifepod/unlock entities — the SAME
/// application runs on the host (TrapEffectApplier) and on the replaying
/// guests (TrapVisualReplay): find the entity at the event position, apply the
/// transition, the entity's own Update/animation drives the rest. Each action
/// mirrors the game's own code path for the transition (the trigger side ran
/// the original) and returns whether it APPLIED — false means the local copy
/// already consumed the one-shot transition (a duplicate event — the
/// two-trigger race: two guests trip the same mine/shower/terminal almost
/// simultaneously; both report, both receive the other's relay; the receiver
/// must DROP what it already did locally, never re-apply it). The caller logs
/// the drop (留痕 — a duplicate that reaches the apply step is evidence).
/// </summary>
internal static class TrapStateActions
{
	/// <summary>Shuttle door: activate + replay the TRIGGER sound (shuttleNotice,
	/// ShuttleStartOpen.cs:53 — the collision-only sound the peers never hear),
	/// then the entity's own Update drives the door animation and the moving
	/// sound (shuttleOpen at 2 s, ShuttleStartOpen.cs:26-30) from the same start
	/// moment on both sides. The 50 % talk is the trigger-side player's local
	/// UI, not replayed.</summary>
	internal static bool ApplyShuttleDoor(ShuttleStartOpen door)
	{
		if (Traverse.Create(door).Field("activated").GetValue<bool>())
		{
			return false; // already consumed — a duplicate event
		}

		Traverse.Create(door).Field("activated").SetValue(true);
		Sound.Play("shuttleNotice", door.transform.position, false, false, null, 1f, 1f, false, false);
		return true;
	}

	/// <summary>Heat button: toggle the controller's heat state until it matches
	/// the trigger side's (ToggleHeatState cycles 0→1→2→0 and writes heater/
	/// desiredTemp/enabled/sprite/description — the game's own write path).
	/// Repeatable — every toggle applies.</summary>
	internal static bool ApplyHeat(LifepodController controller, byte target)
	{
		var guard = 0;
		while (controller.heatState != target && guard++ < 4)
		{
			controller.ToggleHeatState();
		}

		return true;
	}

	/// <summary>Shower button: activate (ActivateShower → shower.Activate + the
	/// disinfect sprite; the shower's own Update cleanses the local real body
	/// for 3 s). The shower's activated flag is the consumption mark.</summary>
	internal static bool ApplyShower(LifepodController controller)
	{
		if (controller.shower != null && controller.shower.activated) // Unity object — ==
		{
			return false; // already consumed — a duplicate event
		}

		controller.ActivateShower();
		return true;
	}

	/// <summary>Blood terminal unlocked: Backgroundify the terminal and every
	/// reinforceddoor in a 6 m radius (BioTerminalScript.cs:33-43, minus the
	/// blood consumption — that already happened on the trigger side). The
	/// terminal's disabled collider is the consumption mark.</summary>
	internal static bool ApplyBioTerminal(BioTerminalScript terminal)
	{
		var building = terminal.GetComponent<BuildingEntity>(); // the private field's value (BioTerminalScript.Start)
		if (building != null) // Unity object — ==
		{
			var collider = building.GetComponent<Collider2D>();
			if (collider != null && !collider.enabled) // Unity object — ==
			{
				return false; // already consumed — a duplicate event
			}

			building.Backgroundify();
		}

		Sound.Play("beep", terminal.transform.position, false, true, null, 1f, 1f, false, false);
		BackgroundifyNearbyDoors(terminal.transform.position, 6f);
		return true;
	}

	/// <summary>Scrap eater fed: write the progress (the Update writes the
	/// description from scrapAmount every frame); at 100 % run the unlock
	/// (Backgroundify + the 2 m doors + beep — ScrapEaterScript.cs:27-39).
	/// A PROGRESS event always applies (every feed reports the new gauge);
	/// the unlock part is idempotent (Backgroundify re-runs are no-ops).</summary>
	internal static bool ApplyScrapEater(ScrapEaterScript eater, byte progress)
	{
		eater.scrapAmount = progress / 100f * ScrapEaterScript.target;
		if (progress < 100)
		{
			return true;
		}

		if (eater.build != null) // Unity object — ==
		{
			eater.build.Backgroundify();
		}

		Sound.Play("beep", eater.transform.position, false, true, null, 1f, 1f, false, false);
		BackgroundifyNearbyDoors(eater.transform.position, 2f);
		return true;
	}

	/// <summary>Med station triggered: mark didHeal + sound + Backgroundify
	/// (MedStationScript.cs:24-27). A LOCAL real body standing in the station
	/// gets the same treatment as the trigger side's (the laser anim + heal —
	/// copied from HealBody, MedStationScript.cs:32-61; the station is a shared
	/// one-shot, both sides' players in it benefit together).</summary>
	internal static bool ApplyMedStation(MedStationScript station)
	{
		if (Traverse.Create(station).Field("didHeal").GetValue<bool>())
		{
			return false; // already consumed — a duplicate event
		}

		Traverse.Create(station).Field("didHeal").SetValue(true);
		Sound.Play("medicalstation", station.transform.position, false, true, null, 1f, 1f, false, false);
		if (station.build != null) // Unity object — ==
		{
			station.build.Backgroundify();
		}

		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body != null && Vector2.Distance(station.transform.position, body.transform.position) < 2.5f)
		{
			station.StartCoroutine(HealAnimation(station, body));
		}

		return true;
	}

	/// <summary>Battery charger used: consume the firstTime mp3 gift and replay
	/// the insert sound (the insert itself rides the item domain — the battery
	/// IS a world item, its position/condition sync there; only the one-shot
	/// gift and the sound need the event).</summary>
	internal static bool ApplyBattery(BatteryRecharger recharger)
	{
		if (!Traverse.Create(recharger).Field("firstTime").GetValue<bool>())
		{
			return false; // already consumed — a duplicate event
		}

		Traverse.Create(recharger).Field("firstTime").SetValue(false);
		Sound.Play("batteryinsert", recharger.transform.position, false, true, null, 1f, 1f, false, false);
		return true;
	}

	/// <summary>Spikestabber: run the one-shot Stab() — the game's own anim/sound/
	/// activated; the CheckStab frame callback then hurts the local real bodies
	/// above, exactly like the trigger side.</summary>
	internal static bool ApplySpike(SpikeStabberScript spike)
	{
		if (Traverse.Create(spike).Field("activated").GetValue<bool>())
		{
			return false; // already consumed — a duplicate event
		}

		spike.Stab();
		return true;
	}

	/// <summary>Stalactite: run the one-shot Drop() — the spike falls; its
	/// DamagingCrate hurts whatever it lands on (the local real bodies).</summary>
	internal static bool ApplyStalactite(StalactiteDropper dropper)
	{
		if (Traverse.Create(dropper).Field("dropped").GetValue<bool>())
		{
			return false; // already consumed — a duplicate event
		}

		dropper.Drop();
		return true;
	}

	/// <summary>Geyser (repeatable — the game's OWN cooldown gate is the check:
	/// TryRumble returns while rumbling or within 10 s of an activation, so a
	/// duplicate event is dropped by the game's state machine). The event
	/// arrives at the RUMBLE START (the report rides the true event start,
	/// 2026-08-10): re-running TryRumble replays the 1 s forewarning (sound +
	/// shake) in sync with the trigger side, and both sides' Updates erupt
	/// together — this is the sync, not a re-rumble. The liquid type is NOT
	/// part of the event: it was bound at generation time by the host
	/// (GeyserStateSnapshot, #128) — the spout just runs.</summary>
	internal static bool ApplyGeyser(GeyserScript geyser)
	{
		geyser.TryRumble();
		return true;
	}

	/// <summary>Mine pressed: the transient 0.8 s pre-explosion visual —
	/// pressedSprite + the native "mine" sound (MineScript.cs:44-51). The
	/// game's private `pressed` latch is deliberately NOT written: setting it
	/// would make the local MineScript.Update count down and explode the mine
	/// naturally, double-applying the world effects that the MineExploded event
	/// already replays. The MinePressReplayMarker owns the duplicate guard for
	/// this transient event (a second guest's report of the same press must not
	/// replay the sprite/sound again).</summary>
	internal static bool ApplyMinePressed(MineScript mine)
	{
		if (Traverse.Create(mine).Field("pressed").GetValue<bool>()
			|| Traverse.Create(mine).Field("exploded").GetValue<bool>()
			|| mine.GetComponent<MinePressReplayMarker>() != null) // Unity object — ==
		{
			return false; // already triggered/consumed/replayed — a duplicate
		}

		mine.GetComponent<SpriteRenderer>().sprite = mine.pressedSprite;
		Sound.Play("mine", mine.transform.position, false, true, null, 1f, 1f, false, false);
		mine.gameObject.AddComponent<MinePressReplayMarker>();
		return true;
	}

	/// <summary>Sound cannon: consume the one-shot spent + cancel the charge,
	/// and replay the blast sound — sonarouch is a 2D GLOBAL sound
	/// (SoundCannon.cs:64), so the peers hear the blast at any distance. The
	/// blast DAMAGE is not replayed: it is the trigger-side player's
	/// single-target effect and rides the CharacterData report. The deafening
	/// UI (hearing loss etc.) happened on the triggering side.</summary>
	internal static bool ApplySoundCannon(SoundCannon cannon)
	{
		if (Traverse.Create(cannon).Field("spent").GetValue<bool>())
		{
			return false; // already consumed — a duplicate event
		}

		Traverse.Create(cannon).Field("spent").SetValue(true);
		Traverse.Create(cannon).Field("charging").SetValue(false);
		Sound.Play("sonarouch", cannon.transform.position, true, false, null, 1f, 1f, false, true);
		return true;
	}

	/// <summary>Cave-tick nest: consume the one-shot started — stop the particles
	/// and kill the nest (the 16 spiders ride the EntitySpawned channel + runtime
	/// enemy binding).</summary>
	internal static bool ApplyCaveTicks(CaveTickSpawner nest)
	{
		if (Traverse.Create(nest).Field("started").GetValue<bool>())
		{
			return false; // already consumed — a duplicate event
		}

		Traverse.Create(nest).Field("started").SetValue(true);
		var particles = nest.GetComponent<ParticleSystem>();
		if (particles != null) // Unity object — ==
		{
			particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
		}

		Object.Destroy(nest.gameObject, 10f);
		Sound.Play("caveticks", Vector2.zero, true, false, null, 0.7f, 1f, false, false);
		return true;
	}

	/// <summary>Beartrap CLAMPED (repeatable — the activated flag is the current-
	/// clamp mark, cleared on release): close the visual (closeSprite + sound +
	/// shake + the child teeth). The clamp's limb damage happened on the
	/// triggering side (its OWN limb is clamped); the peers see the closed trap.</summary>
	internal static bool ApplyBearTrapClamped(BearTrap trap)
	{
		if (Traverse.Create(trap).Field("activated").GetValue<bool>())
		{
			return false; // already clamped — a duplicate event
		}

		Traverse.Create(trap).Field("activated").SetValue(true);
		Sound.Play("beartrap", trap.transform.position, false, false, null, 1f, 1f, false, false);
		trap.GetComponent<SpriteRenderer>().sprite = trap.closeSprite;
		if (trap.transform.childCount > 0)
		{
			Object.Destroy(trap.transform.GetChild(0).gameObject);
		}

		PlayerCamera.main.shaker.Shake(50f);
		return true;
	}

	/// <summary>Beartrap RELEASED (the caught body stood up on the trigger side):
	/// restore the visual (origSprite + unlatch sound).</summary>
	internal static bool ApplyBearTrapReleased(BearTrap trap)
	{
		if (!Traverse.Create(trap).Field("activated").GetValue<bool>())
		{
			return false; // already open — a duplicate event
		}

		Traverse.Create(trap).Field("activated").SetValue(false);
		Sound.Play("beartrapunlatch", trap.transform.position, false, true, null, 1f, 1f, false, false);
		var orig = Traverse.Create(trap).Field("origSprite").GetValue<Sprite>();
		if (orig != null) // Unity object — ==
		{
			trap.GetComponent<SpriteRenderer>().sprite = orig;
		}

		return true;
	}

	// ---- Visual family (repeatable — the game's own cooldown gates are the
	// checks; a duplicate application is a harmless re-sound/re-sprite) ----

	/// <summary>Barbed fence hit: hitSprite + fence sound.</summary>
	internal static bool ApplyBarbedFence(BarbedFence fence)
	{
		fence.GetComponent<SpriteRenderer>().sprite = fence.hitSprite;
		Sound.Play("fence", fence.transform.position, false, true, null, 1f, 1f, false, false);
		return true;
	}

	/// <summary>Coil shock: zap + light flash (the intensity decays back on its
	/// own) + shake.</summary>
	internal static bool ApplyCoil(CoilScript coil)
	{
		SetLightIntensity(coil, 1f);
		Sound.Play("zap", coil.transform.position, false, true, null, 1f, 1f, false, false);
		PlayerCamera.main.shaker.Shake(200f);
		return true;
	}

	/// <summary>Cactus hit: the gore sound (the cactus's own self-damage stays
	/// local, a recorded small divergence).</summary>
	internal static bool ApplyCactus(CactusScript cactus)
	{
		Sound.Play($"gore{Random.Range(1, 6)}", cactus.transform.position, false, true, null, 1f, 1f, false, false);
		return true;
	}

	/// <summary>Jump pad launch: light flash + jumppad sound + shake.</summary>
	internal static bool ApplyJumpPad(JumpPadScript pad)
	{
		SetLightIntensity(pad, 1f);
		Sound.Play("jumppad", pad.transform.position, false, true, null, 1f, 1f, false, false);
		PlayerCamera.main.shaker.Shake(70f);
		return true;
	}

	/// <summary>Banana slip: the plantslip sound.</summary>
	internal static bool ApplyBananaSlip(BananaPlantSlip plant)
	{
		Sound.Play("plantslip", plant.transform.position, false, true, null, 1f, 1f, false, false);
		return true;
	}

	/// <summary>Turret fired: the rifleshot sound, then consume the fire state on
	/// the peer's copy — timeSinceFired = 0 + didShoot = true start its 15 s
	/// reload (TurretScript.cs:30-53), so a peer walking into range gets beeped
	/// but NOT shot during the reload, matching the triggering side. The tracer
	/// beam is a local LineRenderer — recorded gap.</summary>
	internal static bool ApplyTurretFired(TurretScript turret)
	{
		// The timeline decision (warning → 3 s / firing → 0 s / 15 s reload) is
		// the pure TurretReplayTimeline — the #131 timing fix, locked by tests;
		// this method only binds it to the game fields and plays the sounds.
		// At t = 0 the warning (turretsee, TurretScript.cs:69 — 2D) — the
		// trigger side's discovery moment; the post-fire STATE is set
		// immediately (didShoot starts the 15 s reload, didBeep pins the
		// discovery branch off — the 0.5 s window is silent like single-player
		// and the game's own Update can never fire a REAL shot, the `!didShoot`
		// guard). timeSinceFired = 3 s at the warning keeps the fire skin/lamp
		// dark until the 0.5 s coroutine sets 0 at the firing moment.
		var timeline = TurretReplayTimeline.OnWarning();
		Sound.Play("turretsee", turret.transform.position, true, false, null, 1f, 1f, false, false);
		Traverse.Create(turret).Property("timeSinceFired").SetValue(timeline.TimeSinceFired);
		Traverse.Create(turret).Field("didShoot").SetValue(timeline.DidShoot);
		Traverse.Create(turret).Field("didBeep").SetValue(timeline.DidBeep);
		turret.StartCoroutine(DelayedFireVisuals(turret));
		return true;
	}

	/// <summary>The shot visual 0.5 s after the warning — the trigger side's
	/// beepTime firing moment (TurretScript.cs:40). Pure visual (no Shoot() —
	/// the shot's damage is the trigger-side player's single-target effect).
	/// timeSinceFired = 0 lands HERE, at the firing moment — the fireSprite
	/// skin and the fireLamp are driven by it (TurretScript.cs:26-28) and must
	/// light when the trigger side's shot does, not at the warning.</summary>
	private static IEnumerator DelayedFireVisuals(TurretScript turret)
	{
		yield return new WaitForSeconds(0.5f);
		if (turret == null) // Unity object — ==
		{
			yield break; // destroyed while the delay ran
		}

		Traverse.Create(turret).Property("timeSinceFired").SetValue(TurretReplayTimeline.FiringTimeSinceFired); // the firing moment: fire skin + lamp + shot visuals together
		Sound.Play("rifleshot", turret.transform.position, true, false, null, 1f, 1f, false, false);
		var particles = turret.GetComponentInChildren<ParticleSystem>();
		if (particles != null) // Unity object — ==
		{
			particles.Play();
		}

		var tracer = Object.Instantiate(Resources.Load("Special/TurretLine"), Vector2.zero, Quaternion.identity) as GameObject;
		if (tracer != null) // Unity object — ==
		{
			var line = tracer.GetComponent<LineRenderer>();
			var pos = turret.barrel != null ? (Vector2)turret.barrel.position : (Vector2)turret.transform.position; // Unity object — ==
			var end = pos + (Vector2)(turret.transform.right * turret.transform.localScale.x) * 200f;
			line.SetPosition(0, pos);
			line.SetPosition(1, end);
			Object.Destroy(tracer, 0.05f);
		}
	}

	/// <summary>Write the private Light2D field's intensity through reflection
	/// (the Light2D type lives in the URP assembly — not in the reference graph;
	/// the game's own Update decays the intensity back).</summary>
	private static void SetLightIntensity(Component owner, float intensity)
	{
		var light = Traverse.Create(owner).Field("light").GetValue();
		if (light == null)
		{
			return;
		}

		Traverse.Create(light).Property("intensity").SetValue(intensity);
	}

	/// <summary>Backgroundify every reinforceddoor near a position (shared by the
	/// bio terminal and the scrap eater unlocks).</summary>
	private static void BackgroundifyNearbyDoors(Vector2 position, float radius)
	{
		foreach (var collider in Physics2D.OverlapCircleAll(position, radius))
		{
			if (collider.TryGetComponent<BuildingEntity>(out var entity) && entity.id == "reinforceddoor")
			{
				entity.Backgroundify();
			}
		}
	}

	/// <summary>The med station's laser-heal coroutine, copied from
	/// MedStationScript.HealBody (MedStationScript.cs:32-61 — Copy source:
	/// Assembly-CSharp, reverse-engineering 2026-08-10).</summary>
	private static IEnumerator HealAnimation(MedStationScript station, Body body)
	{
		var line = station.GetComponent<LineRenderer>();
		var time = 0f;
		if (line != null) // Unity object — ==
		{
			line.enabled = true;
		}

		while (time < 3f)
		{
			time += Time.deltaTime;
			if (line != null)
			{
				line.SetPosition(0, station.transform.position);
				line.SetPosition(1, body.limbs[1].transform.position);
			}

			yield return null;
		}

		body.hunger = Mathf.Max(body.hunger, Mathf.Lerp(body.hunger, 100f, 0.5f));
		body.thirst = Mathf.Max(body.thirst, Mathf.Lerp(body.thirst, 100f, 0.5f));
		body.GetComponent<Painkillers>()?.opiateAmount += 30f;
		body.happiness += 4f;
		body.sicknessAmount *= 0.5f;
		body.stamina += 50f;
		body.energy += 30f;
		foreach (var limb in body.limbs)
		{
			limb.bleedAmount *= 0.5f;
			limb.muscleHealth = Mathf.Lerp(limb.muscleHealth, 100f, 0.3f);
			limb.skinHealth = Mathf.Lerp(limb.skinHealth, 100f, 0.3f);
			limb.boneHealTimer *= 0.5f;
			limb.dislocationTimer *= 0.5f;
		}

		if (line != null)
		{
			line.enabled = false;
		}
	}
}
