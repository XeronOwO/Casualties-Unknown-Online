using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.GameAdapter.Capabilities;

/// <summary>
/// One capability group: a stable id, its Required/Optional class, the patch
/// classes that form its install/uninstall unit, the hand-declared dynamic rows
/// it owns, and the game types/members it reads outside any patch contract.
///
/// The install unit is the patch set because a capability must be installable
/// and removable on its own from stage 3 on; stage 1 declares the unit and its
/// probe result while the installer still applies the whole assembly. Patch
/// owners are the CONTAINER types the patches were authored in (one file per
/// gameplay area, several patch classes per container), so the declaration is
/// compile-bound and a patch class can never be added without a capability.
/// </summary>
internal sealed class AdapterCapabilityDefinition
{
	internal AdapterCapabilityDefinition(
		string id,
		string title,
		AdapterCapabilityKind kind,
		Type[] patchOwners,
		string[] dynamicPatchClasses,
		Type[] gameTypes,
		AdapterMemberProbe[] gameMembers)
	{
		Id = id;
		Title = title;
		Kind = kind;
		PatchOwners = patchOwners;
		DynamicPatchClasses = dynamicPatchClasses;
		GameTypes = gameTypes;
		GameMembers = gameMembers;
	}

	internal string Id { get; }

	internal string Title { get; }

	internal AdapterCapabilityKind Kind { get; }

	/// <summary>The types whose <c>[HarmonyPatch]</c> classes (nested ones included) form this capability's install unit.</summary>
	internal IReadOnlyList<Type> PatchOwners { get; }

	/// <summary>The dynamic contract rows this capability owns, by <c>PatchInventory</c>'s pseudo patch-class name.</summary>
	internal IReadOnlyList<string> DynamicPatchClasses { get; }

	/// <summary>Game types this capability needs beyond its patch targets, resolved by reflection at probe time.</summary>
	internal IReadOnlyList<Type> GameTypes { get; }

	/// <summary>Game members read outside a patch contract, resolved by reflection at probe time.</summary>
	internal IReadOnlyList<AdapterMemberProbe> GameMembers { get; }
}
