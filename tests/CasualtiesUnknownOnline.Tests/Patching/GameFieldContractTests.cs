using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The Traverse-accessed game members' type contracts — the silent-failure
/// guard for the reflection reads/writes. Traverse.GetValue&lt;T&gt; requires the
/// EXACT field type (a byte field read as int threw InvalidCastException and
/// the geyser report died silently — 1545837; a SetValue with the wrong type
/// killed the peer's rumble+spout, 71a804a). EVERY field/property the adapter
/// touches through reflection is declared here with its exact game type — a
/// game update that renames or retypes a member fails the test run BEFORE the
/// game launches, instead of killing the report at runtime.
///
/// The ExpectedType column is three-state: a <see cref="Type"/> (BCL types the
/// test project can compile against — exact match), a string (a game/Unity
/// type NAME, resolved via <see cref="GameAssemblyHost.ResolveType"/> — exact
/// match; an unresolvable name is itself a violation, never a skip), or null
/// (existence-only — members read WITHOUT a generic type argument, where the
/// adapter does not depend on the type).
/// </summary>
public class GameFieldContractTests
{
	private enum Kind
	{
		Field,
		Property,
	}

	/// <summary>The declared contracts — one row per Traverse-accessed member.
	/// The type is what the game assembly declares TODAY; the adapter's
	/// GetValue/SetValue calls must keep matching it exactly.</summary>
	private static readonly (string Type, string Member, Kind MemberKind, object? ExpectedType, string Why)[] Declared =
	[
		// Geyser — the historical silent-kill guards.
		("GeyserScript", "liquidType", Kind.Field, typeof(byte), "read as byte — a GetValue<int> threw InvalidCastException and killed the geyser report (1545837)"),
		("GeyserScript", "rumbleTime", Kind.Field, typeof(float), "the TryRumble transition edge (TrapGeyserPatch)"),
		// Turret.
		("TurretScript", "timeSinceFired", Kind.Property, typeof(float), "the fire skin/lamp timeline (TrapStateActions, #131)"),
		("TurretScript", "didShoot", Kind.Field, typeof(bool), "the reload latch (TrapStateActions)"),
		("TurretScript", "didBeep", Kind.Field, typeof(bool), "the discovery-branch latch (TrapStateActions)"),
		("TurretScript", "explodeCount", Kind.Field, typeof(float), "the self-destruct accumulation (TrapTurretPatch.SelfDestructPatch)"),
		("TurretScript", "build", Kind.Field, "BuildingEntity", "the building ref — health kill + collider disable (TrapEffectApplier, TrapVisualReplay)"),
		// The one-shot trap latches (TrapStateActions + the trigger patches + the guest replays).
		("ShuttleStartOpen", "activated", Kind.Field, typeof(bool), "the door's one-shot latch (TrapStateActions.ApplyShuttleDoor, TrapShuttleDoorPatch, TrapVisualReplay)"),
		("ShuttleStartOpen", "progress", Kind.Field, typeof(float), "the door's animation time — the elapsed-jump replay sets it to land at the host's current state (TrapVisualReplay.ReplayShuttleDoor)"),
		("ShuttleStartOpen", "playedSound", Kind.Field, typeof(bool), "the 2 s pre-warning sound latch, set by the elapsed replay (TrapVisualReplay)"),
		("ShuttleStartOpen", "didTalk", Kind.Field, typeof(bool), "the 4 s talk latch, set by the elapsed replay (TrapVisualReplay)"),
		("SpikeStabberScript", "activated", Kind.Field, typeof(bool), "the spike's one-shot latch (TrapStateActions.ApplySpike, TrapVisualReplay.ReplaySpike)"),
		("MedStationScript", "didHeal", Kind.Field, typeof(bool), "the med-station one-shot (TrapStateActions.ApplyMedStation, TrapMedStationPatch)"),
		("BatteryRecharger", "firstTime", Kind.Field, typeof(bool), "the charger's one-shot (TrapStateActions.ApplyBattery, TrapBatteryRechargerPatch)"),
		("StalactiteDropper", "dropped", Kind.Field, typeof(bool), "the dropper's one-shot (TrapStateActions.ApplyStalactite)"),
		("SoundCannon", "spent", Kind.Field, typeof(bool), "the cannon's one-shot (TrapStateActions.ApplySoundCannon, TrapSoundCannonPatch)"),
		("SoundCannon", "charging", Kind.Field, typeof(bool), "the charging latch reset on fire (TrapStateActions.ApplySoundCannon)"),
		("CaveTickSpawner", "started", Kind.Field, typeof(bool), "the nest's one-shot (TrapStateActions.ApplyCaveTicks, TrapCaveTickSpawnerPatch)"),
		("BearTrap", "activated", Kind.Field, typeof(bool), "the clamp/release latch (TrapStateActions, TrapBearTrapPatch)"),
		("MineScript", "exploded", Kind.Field, typeof(bool), "the mine's one-shot — set BEFORE the remote death so OnDestroy skips the chain explosion (TrapEffectApplier, TrapMineExplosionPatch)"),
		// The crystal family (internal types, dynamically patched — object __instance).
		("CrystalUnstable", "timer", Kind.Field, typeof(float), "the 5 s pre-explosion ticking (TrapCrystalPatch.UnstableUpdatePatch)"),
		("CrystalMetamorphic", "activated", Kind.Field, typeof(bool), "the touch latch (TrapCrystalPatch.MetamorphicTouchedPatch)"),
		("CrystalShy", "activated", Kind.Field, typeof(bool), "the touch latch (TrapCrystalPatch.ShyTouchedPatch)"),
		("CrystalEMP", "activated", Kind.Field, typeof(bool), "the TryEMP latch (TrapCrystalPatch.EmpTryEMPPatch)"),
		// The trader domain.
		("TraderScript", "desiredPos", Kind.Field, "UnityEngine.Vector2", "the walk target (TradeExecutor)"),
		("TraderScript", "freeAmount", Kind.Field, typeof(int), "the free-give quota (TradeExecutor.Read, TradeStateSync)"),
		("TraderScript", "freeDressing", Kind.Field, typeof(bool), "the dressing-gift latch (TradeExecutor, TradeStateSync)"),
		("TraderScript", "didHug", Kind.Field, typeof(bool), "the hug latch (TradeExecutor, TradeStateSync)"),
		("TraderScript", "build", Kind.Field, "BuildingEntity", "the health state (TradeExecutor.Read)"),
		// The world-defining members (HarmonyTraverse — the FieldOfWorld dynamic-name set).
		("Openable", "code", Kind.Field, typeof(string), "the keypad code — lazy-generated per side otherwise (WorldEventSync.EnsureKeypadCode, EntitySpawnSync)"),
		("WorldGeneration", "runSettings", Kind.Field, typeof(Dictionary<string, object>), "STATIC — the layer-switch source (HarmonyTraverse.ReadRunSettings)"),
		("WorldGeneration", "generatingWorld", Kind.Field, typeof(bool), "the generation flag (HarmonyTraverse.IsGenerating)"),
		("WorldGeneration", "worldBlocks", Kind.Field, typeof(ushort[,]), "the block table (HarmonyTraverse.ReadWorldBlocks)"),
		("WorldGeneration", "loadingObject", Kind.Field, "UnityEngine.GameObject", "the loading-screen holder (HarmonyTraverse.ReadLoadingObject)"),
		("WorldGeneration", "genRects", Kind.Field, "UnityEngine.RectTransform[]", "the loading-screen rects (HarmonyTraverse.ReadGenRects)"),
		("WorldGeneration", "biomeOverride", Kind.Field, "WorldGeneration+OverrideSceneType", "the biome override enum (HarmonyTraverse.ReadBiomeOverride)"),
		("WorldGeneration", "biomeDepth", Kind.Field, typeof(int), "the biome depth (HarmonyTraverse.ReadBiomeDepth)"),
		("WorldGeneration", "totalTraveled", Kind.Field, typeof(int), "the total-traveled counter (HarmonyTraverse.ReadTotalTraveled)"),
		("PreRunScript", "runSettings", Kind.Field, typeof(Dictionary<string, object>), "instance — the menu's run settings (HarmonyTraverse.ReadPreRunRunSettings)"),
		// Enemy sync.
		("SpiderHandler", "biteCooldown", Kind.Field, typeof(float), "the bite gate decremented by RemoteEnemyDriver (the freeze patch skips SpiderHandler.Update, SpiderHandler.cs:39)"),
		// Character/item bodies.
		("Body", "movingAllowed", Kind.Field, typeof(bool), "the start-gate freeze (StartGateCoordinator, BodyPatches)"),
		("LiquidAffect", "rb", Kind.Field, "UnityEngine.Rigidbody2D", "the one-time backfill when a frozen item goes dynamic (ItemPositionFollow)"),
		("GrabberPlant", "grabBody", Kind.Field, "UnityEngine.Rigidbody2D", "the grab latch (TrapGrabberPlantPatch)"),
		// The talker domain.
		("Talker", "currentString", Kind.Field, typeof(string), "the bubble-text latch (TalkerPatch, SpeechSync.Replay)"),
		("Talker", "timeSinceTalked", Kind.Field, typeof(float), "reset on replay so the next bubble fires (SpeechSync.Replay)"),
		("Talker", "text", Kind.Field, null, "the lazily-created TextMeshPro ref — read UNTYPED (SpeechSync.Replay); existence-only, the adapter never depends on its type"),
	];

	[Fact]
	public void DeclaredTable_EveryContractResolvesWithExactType()
	{
		var violations = new List<string>();
		foreach (var (typeName, memberName, memberKind, expected, why) in Declared)
		{
			var type = GameAssemblyHost.ResolveType(typeName);
			if (type == null)
			{
				violations.Add($"type {typeName} not found (renamed?)");
				continue;
			}

			var member = memberKind == Kind.Field
				? (MemberInfo?)type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
				: type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (member == null)
			{
				violations.Add($"{typeName}.{memberName} ({memberKind}) not found (renamed?)");
				continue;
			}

			if (expected == null)
			{
				continue; // existence-only
			}

			var actualType = memberKind == Kind.Field ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
			if (expected is Type expectedType)
			{
				if (actualType != expectedType)
				{
					violations.Add($"{typeName}.{memberName} is {actualType.Name}, the adapter reads/writes {expectedType.Name} — {why}");
				}
			}
			else if (expected is string expectedName)
			{
				var resolved = GameAssemblyHost.ResolveType(expectedName);
				if (resolved == null)
				{
					violations.Add($"{typeName}.{memberName}: expected type '{expectedName}' could not be resolved — check the declared name");
				}
				else if (actualType != resolved)
				{
					violations.Add($"{typeName}.{memberName} is {actualType.Name}, the adapter reads/writes {expectedName} — {why}");
				}
			}
		}

		Assert.True(violations.Count == 0,
			$"game-field contracts broken against the game assembly ({violations.Count}):\n" + string.Join("\n", violations));
	}
}
