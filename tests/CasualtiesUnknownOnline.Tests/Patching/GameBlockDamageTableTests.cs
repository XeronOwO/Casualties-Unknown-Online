using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The adapter's view of the game's OWN partial block-damage table
/// (<c>WorldGeneration.world.blockDamages</c>) — the table a mid-run cut carries
/// with its <c>native-block-damage</c> kind, and the one a restore writes back
/// separately from CUO's bounded registry.
///
/// What is verified HERE is what needs the real game types: the row↔list mapping
/// over a real <c>List&lt;BlockDamage&gt;</c> and the per-row decision rules
/// (air, damage range, the game's own 128-entry cap) against the real block
/// health table. What is NOT verifiable here, and is named as such:
/// - the world READS inside the apply loop: <c>WorldGeneration.GetBlock</c> calls
///   <c>Math.Clamp(long,long,long)</c>, a netstandard-2.1 API the net48 test host
///   does not have, so the loop itself only runs in-game;
/// - everything driven by <c>Object.FindObjectsOfType</c> (the keypad and geyser
///   tables) and the crack-sprite refresh (<c>BlockDamage.UpdateSprite</c> creates
///   a GameObject) — those are exercised by the live guest path, and the restored
///   path by the user's in-game pass.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class GameBlockDamageTableTests
{
	[Fact]
	public void Capture_ReadsTheGameListAsTheWireRowsTheCutWrites()
	{
		var world = CreateWorld(16, 16, block: 1);
		AddDamage(world, 3, 4, 12.5f);
		AddDamage(world, 7, 8, 0.25f);

		var rows = Capture(world);

		Assert.Equal(2, rows.Count);
		Assert.Contains(rows, row => row is { X: 3, Y: 4, Damage: 12.5f });
		Assert.Contains(rows, row => row is { X: 7, Y: 8, Damage: 0.25f });
	}

	[Fact]
	public void Capture_EmptyList_ReadsNoRow() => Assert.Empty(Capture(CreateWorld(8, 8, block: 1)));

	[Fact]
	public void Decide_AppliesASurvivingCellsRowAndANewCellOnlyBelowTheGameCap()
	{
		// Block 1 is lightrock — 100 hp (WorldGeneration.cs:323-331).
		Assert.Equal("Apply", Decide(damage: 50f, block: 1, health: 100f, tracked: false, count: 0));
		Assert.Equal("Apply", Decide(damage: 50f, block: 1, health: 100f, tracked: true, count: 128));
		Assert.Equal("RefuseCap", Decide(damage: 50f, block: 1, health: 100f, tracked: false, count: 128));
		Assert.Equal("RefuseCap", Decide(damage: 50f, block: 1, health: 100f, tracked: false, count: 256));
	}

	[Fact]
	public void Decide_RefusesAirCellsAndDamageOutsideASurvivingBlocksRange()
	{
		Assert.Equal("RefuseAir", Decide(damage: 50f, block: 0, health: 0f, tracked: false, count: 0));
		Assert.Equal("RefuseAir", Decide(damage: 50f, block: 0, health: 0f, tracked: true, count: 0));
		Assert.Equal("RefuseRange", Decide(damage: 100f, block: 1, health: 100f, tracked: false, count: 0)); // at health — that is a break, not partial damage
		Assert.Equal("RefuseRange", Decide(damage: 0f, block: 1, health: 100f, tracked: false, count: 0));
		Assert.Equal("RefuseRange", Decide(damage: -1f, block: 1, health: 100f, tracked: true, count: 0));
	}

	[Fact]
	public void NativeWorldFacts_HoldsTheRestoredTablesUntilTheyAreTakenExactlyOnce()
	{
		var native = CreateNativeWorldFacts();
		var keypads = new List<KeypadEntryMsg> { new() { Position = new NetVector2Msg(1f, 2f), Code = "4821" } };
		var geysers = new List<GeyserStateEntryMsg> { new() { Position = new NetVector2Msg(3f, 4f), LiquidType = 2 } };
		var damages = new List<BlockDamageEntryMsg> { new() { X = 5, Y = 6, Damage = 7f } };

		Assert.False(HasPendingRestore(native), "nothing handed over yet");

		ApplyKeypadCodes(native, keypads);
		ApplyGeysers(native, geysers);
		ApplyBlockDamages(native, damages);

		Assert.True(HasPendingRestore(native), "the restored native values must wait for the world-entry seam");

		var read = ReadPendingRestore(native);
		Assert.NotNull(read);
		Assert.Equal("4821", Assert.Single(PropertyOf<IReadOnlyList<KeypadEntryMsg>>(read!, "Keypads")).Code);
		Assert.Equal(2, Assert.Single(PropertyOf<IReadOnlyList<GeyserStateEntryMsg>>(read!, "Geysers")).LiquidType);
		Assert.Equal(7f, Assert.Single(PropertyOf<IReadOnlyList<BlockDamageEntryMsg>>(read!, "BlockDamages")).Damage);

		// A READ must not consume: a generation that cannot take every value retries
		// with the SAME set, and only an applied replay commits the handover.
		Assert.True(HasPendingRestore(native), "reading the handover must not consume it");

		CommitPendingRestore(native);
		Assert.False(HasPendingRestore(native), "a committed restore must not be handed to a second generation");
		var empty = ReadPendingRestore(native);
		Assert.NotNull(empty);
		Assert.Empty(PropertyOf<IReadOnlyList<KeypadEntryMsg>>(empty!, "Keypads"));
		Assert.Empty(PropertyOf<IReadOnlyList<GeyserStateEntryMsg>>(empty!, "Geysers"));
		Assert.Empty(PropertyOf<IReadOnlyList<BlockDamageEntryMsg>>(empty!, "BlockDamages"));
	}

	// ---- the real game types (Assembly-CSharp / UnityEngine, reflection-only) ----

	private static Type Resolve(string name) =>
		GameAssemblyHost.ResolveType(name) ?? throw new InvalidOperationException($"{name} not found in the loaded game assemblies.");

	private static Type WorldType() => Resolve("WorldGeneration");

	private static object CreateWorld(int width, int height, ushort block)
	{
		var worldType = WorldType();
		var world = FormatterServices.GetUninitializedObject(worldType);
		worldType.GetField("width", BindingFlags.Public | BindingFlags.Instance)!.SetValue(world, (uint)width);
		worldType.GetField("height", BindingFlags.Public | BindingFlags.Instance)!.SetValue(world, (uint)height);

		var blocks = new ushort[width, height];
		for (var x = 0; x < width; x++)
		{
			for (var y = 0; y < height; y++)
			{
				blocks[x, y] = block;
			}
		}

		worldType.GetField("worldBlocks", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(world, blocks);
		worldType.GetField("blockDamages", BindingFlags.Public | BindingFlags.Instance)!.SetValue(world, NewDamageList());
		return world;
	}

	private static IList DamageList(object world) =>
		(IList)WorldType().GetField("blockDamages", BindingFlags.Public | BindingFlags.Instance)!.GetValue(world)!;

	private static IList NewDamageList() =>
		(IList)Activator.CreateInstance(WorldType().GetField("blockDamages", BindingFlags.Public | BindingFlags.Instance)!.FieldType)!;

	private static void AddDamage(object world, int x, int y, float damage)
	{
		var damageType = Resolve("BlockDamage");
		var entry = Activator.CreateInstance(damageType)!;
		damageType.GetField("pos", BindingFlags.Public | BindingFlags.Instance)!.SetValue(entry, Activator.CreateInstance(Resolve("UnityEngine.Vector2Int"), x, y));
		damageType.GetField("damage", BindingFlags.Public | BindingFlags.Instance)!.SetValue(entry, damage);
		DamageList(world).Add(entry);
	}

	// ---- the adapter methods (reflection: the test project never references the adapter assembly) ----

	private static Type AdapterType(string name) =>
		GameAssemblyHost.Adapter.GetType(name) ?? throw new InvalidOperationException($"{name} not found in the GameAdapter assembly.");

	private static Type TableType() => AdapterType("CasualtiesUnknownOnline.GameAdapter.World.GameBlockDamageTable");

	private static MethodInfo TableMethod(string name) =>
		TableType().GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException($"GameBlockDamageTable.{name} not found.");

	private static IReadOnlyList<BlockDamageEntryMsg> Capture(object world) =>
		(IReadOnlyList<BlockDamageEntryMsg>)TableMethod("Capture").Invoke(null, [world])!;

	/// <summary>The verdict name, so the assertion reads as the rule and not as an enum ordinal.</summary>
	private static string Decide(float damage, ushort block, float health, bool tracked, int count) =>
		TableMethod("Decide").Invoke(null, [damage, block, health, tracked, count])!.ToString()!;

	// ---- the native world-fact port (pending handover, take-once) ----

	private static object CreateNativeWorldFacts()
	{
		var type = AdapterType("CasualtiesUnknownOnline.GameAdapter.World.NativeWorldFacts");
		var factory = LoggerFactory.Create(_ => { });
		var createLogger = typeof(LoggerFactoryExtensions)
			.GetMethods()
			.Single(method => method.Name == "CreateLogger" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1)
			.MakeGenericMethod(type);
		return Activator.CreateInstance(type, createLogger.Invoke(null, [factory]))!;
	}

	private static void ApplyKeypadCodes(object native, IReadOnlyList<KeypadEntryMsg> codes) =>
		NativeMethod(native, "ApplyKeypadCodes").Invoke(native, [codes]);

	private static void ApplyGeysers(object native, IReadOnlyList<GeyserStateEntryMsg> geysers) =>
		NativeMethod(native, "ApplyGeysers").Invoke(native, [geysers]);

	private static void ApplyBlockDamages(object native, IReadOnlyList<BlockDamageEntryMsg> damages) =>
		NativeMethod(native, "ApplyBlockDamages").Invoke(native, [damages]);

	private static bool HasPendingRestore(object native) => PropertyOf<bool>(native, "HasPendingRestore");

	private static object? ReadPendingRestore(object native) => NativeMethod(native, "ReadPendingRestore").Invoke(native, null);

	private static void CommitPendingRestore(object native) => NativeMethod(native, "CommitPendingRestore").Invoke(native, null);

	private static MethodInfo NativeMethod(object native, string name) =>
		native.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException($"NativeWorldFacts.{name} not found.");

	private static T PropertyOf<T>(object target, string name)
	{
		var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"{target.GetType().Name}.{name} not found.");
		return (T)property.GetValue(target)!;
	}
}
