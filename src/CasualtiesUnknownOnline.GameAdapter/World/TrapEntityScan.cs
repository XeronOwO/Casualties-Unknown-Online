using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The sync-domain trap entity scan — the SHARED component table for the
/// host's layout scanner and the guest's local layout enumeration: one row
/// per component type (scanned with the non-generic FindObjectsOfType — the
/// crystal family's effects are plain classes, not components, so the public
/// CrystalBehaviour base is scanned and the row's own comment states what that
/// currently yields), each instance may produce several layout entries (a turret
/// is both the TurretFired and the TurretSelfDestructed position). The prefab name is the
/// instance's own scene name with the "(Clone)" suffix stripped — the host's
/// scene IS the fact, never a hand-built kind→prefab table; a structural
/// member (its name carries no Clone suffix, e.g. the shuttle door inside the
/// drill pod) fails the guest's Resources.Load and is skipped with a trace —
/// structural members depend on the structure's deterministic placement.
/// </summary>
internal static class TrapEntityScan
{
	private readonly struct Row
	{
		internal Type Component { get; init; }

		internal Func<Component, EntityEventKind[]> Kinds { get; init; }
	}

	private static readonly Row[] Rows =
	[
		new Row { Component = typeof(MineScript), Kinds = _ => [EntityEventKind.MinePressed, EntityEventKind.MineExploded] },
		new Row { Component = typeof(SpikeStabberScript), Kinds = _ => [EntityEventKind.SpikeStabbed] },
		new Row { Component = typeof(BearTrap), Kinds = _ => [EntityEventKind.BearTrapClamped, EntityEventKind.BearTrapReleased] },
		new Row { Component = typeof(BarbedFence), Kinds = _ => [EntityEventKind.BarbedFenceHit] },
		new Row { Component = typeof(CoilScript), Kinds = _ => [EntityEventKind.CoilShocked] },
		new Row { Component = typeof(CactusScript), Kinds = _ => [EntityEventKind.CactusHit] },
		new Row { Component = typeof(JumpPadScript), Kinds = _ => [EntityEventKind.JumpPadLaunched] },
		new Row { Component = typeof(StalactiteDropper), Kinds = _ => [EntityEventKind.StalactiteDropped] },
		new Row { Component = typeof(GeyserScript), Kinds = _ => [EntityEventKind.GeyserActivated] },
		new Row { Component = typeof(SoundCannon), Kinds = _ => [EntityEventKind.SoundCannonFired] },
		new Row { Component = typeof(TurretScript), Kinds = _ => [EntityEventKind.TurretFired, EntityEventKind.TurretSelfDestructed] },
		new Row { Component = typeof(CrystalBehaviour), Kinds = CrystalKinds },
		new Row { Component = typeof(CaveTickSpawner), Kinds = _ => [EntityEventKind.CaveTicksSpawned] },
		new Row { Component = typeof(BananaPlantSlip), Kinds = _ => [EntityEventKind.BananaPlantSlip] },
		new Row { Component = typeof(GrabberPlant), Kinds = _ => [EntityEventKind.GrabberGrabbed] },
		new Row { Component = typeof(ShuttleStartOpen), Kinds = _ => [EntityEventKind.ShuttleDoorOpened] },
		new Row { Component = typeof(LifepodController), Kinds = _ => [EntityEventKind.LifepodHeatChanged, EntityEventKind.LifepodShowerActivated] },
		new Row { Component = typeof(BioTerminalScript), Kinds = _ => [EntityEventKind.BioTerminalUnlocked] },
		new Row { Component = typeof(ScrapEaterScript), Kinds = _ => [EntityEventKind.ScrapEaterProgress] },
		new Row { Component = typeof(MedStationScript), Kinds = _ => [EntityEventKind.MedStationHealed] },
		new Row { Component = typeof(BatteryRecharger), Kinds = _ => [EntityEventKind.BatteryInserted] },
	];

	/// <summary>One scanned local entity: the layout entry plus the live component (the application resolves it for destroys).</summary>
	internal readonly struct Scanned
	{
		internal TrapLayoutEntryMsg Entry { get; init; }

		internal Component Component { get; init; }
	}

	/// <summary>Enumerate every sync-domain trap entity in the scene.</summary>
	internal static List<Scanned> Scan()
	{
		var result = new List<Scanned>();
		foreach (var row in Rows)
		{
			foreach (var found in Object.FindObjectsOfType(row.Component))
			{
				var component = (Component)found;
				if (component == null) // Unity object — ==
				{
					continue;
				}

				foreach (var kind in row.Kinds(component))
				{
					result.Add(new Scanned
					{
						Entry = new TrapLayoutEntryMsg
						{
							Kind = kind,
							X = component.transform.position.x,
							Y = component.transform.position.y,
							PrefabName = PrefabNameOf(component),
							// A runtime-created trap/mechanism carries the
							// creation marker (EntitySpawnSync stamps every
							// runtime creation); carrying its key lets the
							// guest's materialized copy hold the SAME identity
							// as the live copy, so the runtime-entity snapshot
							// binds by key instead of proximity. The marker sits
							// on the entity's GameObject while some trap scripts
							// (GeyserScript) hang on a CHILD object, so the lookup
							// walks up the transform chain.
							CreationKey = RuntimeEntityCreation.TryReadOnEntityOf(component, out var creationKey)
								? creationKey.ToKeyMsg()
								: null,
						},
						Component = component,
					});
				}
			}
		}

		return result;
	}

	/// <summary>The instance's prefab name: the scene name minus the instantiation
	/// suffix. A structural member (no suffix) keeps its scene name — the guest's
	/// Resources.Load fails on it and the materialization skips it.</summary>
	internal static string PrefabNameOf(Component component)
	{
		var name = component.gameObject.name;
		const string cloneSuffix = "(Clone)";
		return name.EndsWith(cloneSuffix, StringComparison.Ordinal)
			? name.Substring(0, name.Length - cloneSuffix.Length)
			: name;
	}

	/// <summary>The crystal family: the kinds live on the behaviour's private
	/// `effects` list (plain-class CrystalFragile, CrystalUnstable, …), NOT on the
	/// component's type name, which is always `CrystalBehaviour` — so this row
	/// currently yields NO crystal entries, and the layout carries no crystal kind.
	/// Filed as `docs/backlog/todo/trap-layout-crystal-rows-empty.md`; the fix owes
	/// reading the effects and answering whether the host's and the guest's effect
	/// rolls agree, because enabling the rows makes the guest align (and possibly
	/// destroy/materialize) crystals. Non-event crystal kinds (CrystalDripping)
	/// produce nothing.</summary>
	private static EntityEventKind[] CrystalKinds(Component component) => component.GetType().Name switch
	{
		"CrystalFragile" => [EntityEventKind.CrystalFragileBroken],
		"CrystalElectric" => [EntityEventKind.CrystalElectricShocked],
		"CrystalUnstable" => [EntityEventKind.CrystalUnstableTicked, EntityEventKind.CrystalUnstableExploded],
		"CrystalMetamorphic" => [EntityEventKind.CrystalMetamorphicTriggered],
		"CrystalMimic" => [EntityEventKind.CrystalMimicTriggered],
		"CrystalShy" => [EntityEventKind.CrystalShySwapped],
		"CrystalEMP" => [EntityEventKind.CrystalEMPActivated],
		"CrystalTeleport" => [EntityEventKind.CrystalTeleportTriggered],
		_ => [],
	};
}
