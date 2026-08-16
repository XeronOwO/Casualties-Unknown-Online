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
/// crystal family's actual types are internal, the public CrystalBehaviour
/// base is scanned and the kind is derived from the runtime type name), each
/// instance may produce several layout entries (a turret is both the
/// TurretFired and the TurretSelfDestructed position). The prefab name is the
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
		new Row { Component = typeof(MineScript), Kinds = _ => [EntityEventKind.MineExploded] },
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

	/// <summary>The crystal family: the actual types are internal (CrystalFragile,
	/// CrystalElectric, CrystalUnstable, CrystalMetamorphic, CrystalMimic,
	/// CrystalShy, CrystalEMP) — the kind derives from the runtime type name. Non-event
	/// crystal kinds (CrystalDripping) produce nothing.</summary>
	private static EntityEventKind[] CrystalKinds(Component component) => component.GetType().Name switch
	{
		"CrystalFragile" => [EntityEventKind.CrystalFragileBroken],
		"CrystalElectric" => [EntityEventKind.CrystalElectricShocked],
		"CrystalUnstable" => [EntityEventKind.CrystalUnstableExploded],
		"CrystalMetamorphic" => [EntityEventKind.CrystalMetamorphicTriggered],
		"CrystalMimic" => [EntityEventKind.CrystalMimicTriggered],
		"CrystalShy" => [EntityEventKind.CrystalShySwapped],
		"CrystalEMP" => [EntityEventKind.CrystalEMPActivated],
		_ => [],
	};
}
