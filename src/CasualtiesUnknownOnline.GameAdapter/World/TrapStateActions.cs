using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Shared state-family actions for the lifepod/unlock entities — the SAME
/// application runs on the host (TrapEffectApplier) and on the replaying
/// guests (TrapVisualReplay): find the entity at the event position, apply the
/// transition, the entity's own Update/animation drives the rest. Each action
/// is idempotent (a repeated event is a no-op) and mirrors the game's own
/// code path for the transition (the trigger side ran the original).
/// </summary>
internal static class TrapStateActions
{
	/// <summary>Shuttle door: activate — the entity's own Update drives the door
	/// animation from the same start moment on both sides (ShuttleStartOpen.cs:
	/// 14-41, same prefab).</summary>
	internal static void ApplyShuttleDoor(ShuttleStartOpen door) =>
		Traverse.Create(door).Field("activated").SetValue(true);

	/// <summary>Heat button: toggle the controller's heat state until it matches
	/// the trigger side's (ToggleHeatState cycles 0→1→2→0 and writes heater/
	/// desiredTemp/enabled/sprite/description — the game's own write path).</summary>
	internal static void ApplyHeat(LifepodController controller, byte target)
	{
		var guard = 0;
		while (controller.heatState != target && guard++ < 4)
		{
			controller.ToggleHeatState();
		}
	}

	/// <summary>Shower button: activate (ActivateShower → shower.Activate + the
	/// disinfect sprite; Activate is idempotent — a repeated event is a no-op;
	/// the shower's own Update cleanses the local real body for 3 s).</summary>
	internal static void ApplyShower(LifepodController controller) => controller.ActivateShower();

	/// <summary>Blood terminal unlocked: Backgroundify the terminal and every
	/// reinforceddoor in a 6 m radius (BioTerminalScript.cs:33-43, minus the
	/// blood consumption — that already happened on the trigger side).</summary>
	internal static void ApplyBioTerminal(BioTerminalScript terminal)
	{
		var building = terminal.GetComponent<BuildingEntity>(); // the private field's value (BioTerminalScript.Start)
		if (building != null) // Unity object — ==
		{
			building.Backgroundify();
		}

		Sound.Play("beep", terminal.transform.position, false, true, null, 1f, 1f, false, false);
		BackgroundifyNearbyDoors(terminal.transform.position, 6f);
	}

	/// <summary>Scrap eater fed: write the progress (the Update writes the
	/// description from scrapAmount every frame); at 100 % run the unlock
	/// (Backgroundify + the 2 m doors + beep — ScrapEaterScript.cs:27-39).</summary>
	internal static void ApplyScrapEater(ScrapEaterScript eater, byte progress)
	{
		eater.scrapAmount = progress / 100f * ScrapEaterScript.target;
		if (progress < 100)
		{
			return;
		}

		if (eater.build != null) // Unity object — ==
		{
			eater.build.Backgroundify();
		}

		Sound.Play("beep", eater.transform.position, false, true, null, 1f, 1f, false, false);
		BackgroundifyNearbyDoors(eater.transform.position, 2f);
	}

	/// <summary>Med station triggered: mark didHeal + sound + Backgroundify
	/// (MedStationScript.cs:24-27). A LOCAL real body standing in the station
	/// gets the same treatment as the trigger side's (the laser anim + heal —
	/// copied from HealBody, MedStationScript.cs:32-61; the station is a shared
	/// one-shot, both sides' players in it benefit together).</summary>
	internal static void ApplyMedStation(MedStationScript station)
	{
		if (Traverse.Create(station).Field("didHeal").GetValue<bool>())
		{
			return; // already consumed — a repeated event is a no-op
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
	}

	/// <summary>Battery charger used: consume the firstTime mp3 gift (the insert
	/// itself rides the item domain — the battery IS a world item, its
	/// position/condition sync there; only the one-shot gift needs the event).</summary>
	internal static void ApplyBattery(BatteryRecharger recharger) =>
		Traverse.Create(recharger).Field("firstTime").SetValue(false);

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
