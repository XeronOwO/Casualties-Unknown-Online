using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.GameAdapter.WorldGen;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Capabilities;

/// <summary>
/// The adapter capability catalog: which gameplay system each patch belongs to,
/// and whether that system is one the vanilla game itself has (Required) or an
/// addition of ours (Optional). The split follows the user ruling recorded in
/// <c>docs/backlog/todo/adapter-capability-catalog.md</c> — the yardstick is
/// whether the game has the feature, never how hard a capability is to fix.
///
/// The catalog is a declaration, not an installation path: stage 1 uses it to
/// print one report that names the broken system and its reason, while
/// <c>GameAdapter.Install</c> still applies the whole assembly all-or-nothing.
/// A patch class belongs to exactly ONE capability — the file it is authored in
/// is its unit — and the totality gate fails when a new patch class is added
/// without a home or a declared owner stops producing patch classes.
///
/// Optional here means "CUO's own addition", which is why the mod content
/// surface and the diagnostics hooks sit in their own groups: a
/// game update that breaks one of them must be able to leave the session running
/// (stage 2) instead of refusing multiplayer as a whole.
/// </summary>
internal static class AdapterCapabilityCatalog
{
	private static readonly AdapterCapabilityDefinition[] Declared =
	[
		new(
			AdapterCapabilityIds.Session,
			"Session lifecycle, world entry and local input gating",
			AdapterCapabilityKind.Required,
			[
				typeof(PreRunScriptStartPatch), typeof(PreRunScriptStartRunPatch), typeof(PreRunScriptLoadRunPatch),
				typeof(PreRunScriptIntroSkipPatch), typeof(SceneLoadPatches), typeof(PauseHandlerUpdatePatch),
				typeof(PauseHandlerTogglePausePatch), typeof(PlayerCameraHandleInputPatch), typeof(PlayerCameraMenuExitPatches),
				typeof(PlayerCameraSetTimeScalePatch), typeof(PlayerCameraDoAlertPatch),
				typeof(PlayerCameraHandleUnconsciousScreenPatch), typeof(GlobalDarkDarkenPatch), typeof(LifePodShakePatch),
			],
			[],
			// The four types ProbeGame() has always read; the report aggregates that probe
			// here instead of leaving it a separate statement whose result nothing consumes.
			[typeof(PlayerCamera), typeof(Body), typeof(PreRunScript), typeof(WorldGeneration)],
			[]),
		new(
			AdapterCapabilityIds.World,
			"World generation, blocks, fluids and world objects",
			AdapterCapabilityKind.Required,
			[
				typeof(WorldGenerationGenerateWorldPatch), typeof(WorldGenerationUpdatePatch), typeof(WorldGenerationGenerateOresPatch),
				typeof(WorldGenerationPlaceCrystalsPatch), typeof(WorldGenerationGetBlockInfoPatch), typeof(WorldGenerationSetBlockPatch),
				typeof(WorldGenerationDamageBlockPatch), typeof(WorldGenerationStructureDistributionPatch),
				typeof(ExplosionBuildingSyncPatch), typeof(FluidSimulationPatch), typeof(FluidDrinkPatch),
				typeof(BuildingEntityUpdatePatch), typeof(BuildingEntityStartPatch), typeof(OilPipePatch), typeof(LifepodPumpPatch),
				typeof(OpenablePatches), typeof(LockpingSoundPatches),
				// The layer-modifier determinism patch lives in the WorldGen namespace, not
				// in Patches (it is a world-generation boundary, not a gameplay hook).
				typeof(LayerModifierApplyPatch),
			],
			[],
			[],
			[]),
		new(
			AdapterCapabilityIds.Items,
			"Items, inventory, containers, guns and item interaction",
			AdapterCapabilityKind.Required,
			[
				typeof(ItemPatches), typeof(ItemCollisionEnter2DPatch), typeof(BodyItemPatches), typeof(UseItemPatches),
				typeof(ContainerItemPatches), typeof(UtilsCreateDropPatch), typeof(GunStatePatches), typeof(GunFirePatch),
				typeof(HeldItemDirectionPatch), typeof(DynamiteExplodePatch), typeof(PlushScriptCollisionEnter2DPatch),
				typeof(InvButtonBodyPatch), typeof(DoPickupCheckPatch), typeof(LiquidAffectPatches),
				// The drag/radial/wearable seams carry the remote-inventory release and
				// while-dragging windows: the native release and while-dragging bodies run,
				// the windows capture the mutation calls and field stores they make, and the
				// display-body predicates answer the release branch's guards from the body
				// the inventory ring is showing (the vanilla remote-take path keeps this
				// class Required). R10 (the radial centre) is no longer suppressed: its
				// use/wear calls are captured like every other mutation.
				typeof(PlayerCameraDragUsePatch), typeof(PlayerCameraUpdateWearablesPatch),
				typeof(PlayerCameraHandleWhileDraggingPatch), typeof(PlayerCameraRadialActionProbePatch),
				typeof(RemoteDragMutationPatches.RemoteDragContainerLoadPatch), typeof(RemoteDragMutationPatches.RemoteDragContainerUnloadPatch),
				typeof(RemoteDragMutationPatches.RemoteDragLiquidDrainPatch),
				typeof(RemoteDragMutationPatches.RemoteDragBodyPickUpPatch), typeof(RemoteDragMutationPatches.RemoteDragBodySwapSlotsPatch),
				typeof(RemoteDragMutationPatches.RemoteDragBodyDropItemPatch), typeof(RemoteDragMutationPatches.RemoteDragBodyDropSlotPatch),
				typeof(RemoteDragMutationPatches.RemoteDragBodyDropWearablePatch), typeof(RemoteDragMutationPatches.RemoteDragBodyCombinePatch),
				typeof(RemoteDragMutationPatches.RemoteDragBodyUseItemPatch), typeof(RemoteDragMutationPatches.RemoteDragBodyWearPatch),
				typeof(RemoteDragMutationPatches.RemoteDragBatteryLoadPatch), typeof(RemoteDragMutationPatches.RemoteDragBatteryUnloadPatch),
				typeof(RemoteDragMutationPatches.RemoteDragApplyWoundItemPatch), typeof(RemoteDragMutationPatches.RemoteDragTraderGivePatch),
				typeof(RemoteDragPredicatePatches.RemoteDragHoldingItemPatch), typeof(RemoteDragPredicatePatches.RemoteDragHoldingSlotPatch),
				typeof(RemoteDragPredicatePatches.RemoteDragGetItemPatch), typeof(RemoteDragPredicatePatches.RemoteDragGetWearablePatch),
				typeof(RemoteDragPredicatePatches.RemoteDragPickupCheckPatch), typeof(RemoteDragPredicatePatches.RemoteDragOpenContainerPatch),
			],
			[],
			[],
			[]),
		new(
			AdapterCapabilityIds.Traps,
			"Traps, mines and crystals",
			AdapterCapabilityKind.Required,
			[
				typeof(TrapBioTerminalPatch), typeof(TrapBatteryRechargerPatch), typeof(TrapBarbedFencePatch),
				typeof(TrapBananaSlipPatch), typeof(TrapLifepodButtonPatch), typeof(TrapJumpPadPatch),
				typeof(TrapGrabberPlantPatch), typeof(TrapGeyserPatch), typeof(TrapCoilPatch),
				typeof(TrapCaveTickSpawnerPatch), typeof(TrapShuttleDoorPatch), typeof(TrapSoundCannonPatch),
				typeof(TrapScrapEaterPatch), typeof(TrapMinePressPatch), typeof(TrapSpikeStabberPatch),
				typeof(TrapCactusPatch), typeof(TrapStalactitePatch), typeof(TrapMedStationPatch),
				typeof(TrapBearTrapPatch), typeof(TrapMineExplosionPatch), typeof(TrapTurretPatch),
				typeof(TrapCrystalPatch), typeof(MineScriptPatches),
			],
			// The internal crystal types are patched by reflection (no attribute), so their
			// rows are hand-declared in PatchInventory and claimed here by pseudo name.
			[nameof(TrapCrystalPatch) + PatchInventory.DynamicSuffix, nameof(CrystalDrippingPatch) + PatchInventory.DynamicSuffix],
			[],
			[]),
		new(
			AdapterCapabilityIds.Medical,
			"Medical treatment, wounds and the remote medical view",
			AdapterCapabilityKind.Required,
			[
				typeof(LimbStatePatches), typeof(RemoteMedicalPatches), typeof(RemoteShrapnelMinigamePatch),
				typeof(RemoteOtherMedicalMinigamePatch), typeof(BleedParticleWorldBloodPatch),
			],
			[],
			[],
			[]),
		new(
			AdapterCapabilityIds.Character,
			"Body, limb, carry and character presentation",
			AdapterCapabilityKind.Required,
			[
				typeof(BodyPatches), typeof(BodyUpdatePatch), typeof(BodyNapPatch), typeof(BodyWorkoutPatch),
				typeof(CarryEncumbrancePatch), typeof(FacialExpressionHeadPatch), typeof(PantSoundPatches),
				typeof(TalkerPatch), typeof(SoundPlayPatch), typeof(SoundPlayAudioClipPatch),
			],
			[],
			[],
			[]),
		new(
			AdapterCapabilityIds.Enemies,
			"Enemies and their attacks",
			AdapterCapabilityKind.Required,
			[
				typeof(EnemyPatches), typeof(EnemyBitePatches), typeof(EnemyTargetingPatches),
				typeof(EnemyProximityPatches), typeof(EnemyItemHitPatch),
			],
			[],
			[],
			[]),
		new(
			AdapterCapabilityIds.Crafting,
			"Recipes, combination and cooking",
			AdapterCapabilityKind.Required,
			[typeof(CraftingPatches), typeof(HeaterCookPatch)],
			[],
			[],
			[]),
		new(
			AdapterCapabilityIds.Trading,
			"Trader interaction and the trade menu",
			AdapterCapabilityKind.Required,
			[typeof(TraderPatches), typeof(PlayerCameraHandleTradeMenuPatch)],
			[],
			[],
			[]),
		new(
			AdapterCapabilityIds.Save,
			"The game's save/continue surface",
			AdapterCapabilityKind.Required,
			[typeof(SaveSystemHasSavePatch), typeof(SaveSystemTryLoadGamePatch)],
			[],
			[],
			[]),
		new(
			AdapterCapabilityIds.Tutorial,
			"Tutorial entry and the tutorial claw",
			AdapterCapabilityKind.Required,
			[typeof(TutorialHandlerUpdatePatch), typeof(UtilsCreateTutorialPatch), typeof(PreRunScriptStartTutorialPatch)],
			[],
			[],
			[]),
		new(
			AdapterCapabilityIds.Mods,
			"CUO's mod content surface (custom items, liquids, prefabs, statuses)",
			AdapterCapabilityKind.Optional,
			[
				typeof(ModStatusProjectionPatches), typeof(ModStatusMoodlePatches), typeof(ModItemDropSourcePatches),
				typeof(NativeItemResourcePatches), typeof(FluidCustomLiquidPatches), typeof(UtilsCreateCustomPrefabPatch),
				typeof(CustomItemVisualPatches),
			],
			[],
			[],
			[]),
		new(
			AdapterCapabilityIds.Diagnostics,
			"CUO's diagnostic hooks",
			AdapterCapabilityKind.Optional,
			[typeof(PickupFlowDiagnosticsPatch), typeof(ItemUpdateDiagnosticPatch)],
			[],
			[],
			[]),
	];

	private static readonly Lazy<Dictionary<string, string>> PatchClassOwners = new(BuildPatchClassOwners);
	private static readonly Lazy<Dictionary<string, string>> DynamicClassOwners = new(BuildDynamicClassOwners);

	internal static IReadOnlyList<AdapterCapabilityDefinition> All => Declared;

	/// <summary>The type list <c>ProbeGame</c> reads — declared here so the probe and the report cannot disagree.</summary>
	internal static IReadOnlyList<Type> GameProbeTypes =>
		Declared.Single(definition => definition.Id == AdapterCapabilityIds.Session).GameTypes;

	/// <summary>The capability owning a patch class, by full name; null when nothing claims it (reported, never hidden).</summary>
	internal static string? OwnerOfPatchClass(string patchClassType) =>
		PatchClassOwners.Value.TryGetValue(patchClassType, out var id) ? id : null;

	/// <summary>The capability owning a hand-declared dynamic contract row, by pseudo patch-class name.</summary>
	internal static string? OwnerOfDynamicPatchClass(string dynamicPatchClass) =>
		DynamicClassOwners.Value.TryGetValue(dynamicPatchClass, out var id) ? id : null;

	/// <summary>Every <c>[HarmonyPatch]</c> class a definition owns, nested patch classes included.</summary>
	internal static IEnumerable<Type> PatchClassesOf(AdapterCapabilityDefinition definition) =>
		definition.PatchOwners.SelectMany(Expand);

	/// <summary>The capability definition with this id, or null — the ids are asserted unique by the catalog gate.</summary>
	internal static AdapterCapabilityDefinition? Find(string id) =>
		Declared.FirstOrDefault(definition => definition.Id == id);

	private static IEnumerable<Type> Expand(Type owner)
	{
		if (owner.GetCustomAttribute<HarmonyPatch>() is not null)
		{
			yield return owner;
		}

		foreach (var nested in owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
		{
			foreach (var patchClass in Expand(nested))
			{
				yield return patchClass;
			}
		}
	}

	private static Dictionary<string, string> BuildPatchClassOwners()
	{
		var owners = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var definition in Declared)
		{
			foreach (var patchClass in PatchClassesOf(definition))
			{
				// A duplicate claim is a catalog bug the totality gate fails on (every
				// patch class is claimed exactly once), so the runtime keeps the FIRST
				// declaration deterministically instead of a silent last-wins.
				var key = patchClass.FullName ?? patchClass.Name;
				if (!owners.ContainsKey(key))
				{
					owners.Add(key, definition.Id);
				}
			}
		}

		return owners;
	}

	private static Dictionary<string, string> BuildDynamicClassOwners()
	{
		var owners = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var definition in Declared)
		{
			foreach (var dynamicPatchClass in definition.DynamicPatchClasses)
			{
				if (!owners.ContainsKey(dynamicPatchClass))
				{
					owners.Add(dynamicPatchClass, definition.Id);
				}
			}
		}

		return owners;
	}
}
