using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The pure codec face for the persistent states hidden in
/// <c>CustomItemBehaviour.data</c>. The adapter is compile-excluded from the
/// test project, so these tests exercise the helper reflectively (same host
/// as the other GameAdapter contract tests). The focus is the liquidcentrifuge
/// cooldown: an <c>object[]</c> payload that the generic saveable-field codec
/// cannot carry, but which gates a real use action and must survive item
/// transfer/reconnect.
/// </summary>
public class CustomItemDataStateTests
{
	private static readonly Type StateType = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Items.CustomItemDataState",
		throwOnError: true)!;

	private static readonly MethodInfo Capture = StateType.GetMethod(
		"CaptureLiquidCentrifugeCooldown", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CustomItemDataState.CaptureLiquidCentrifugeCooldown not found.");

	private static readonly MethodInfo IsField = StateType.GetMethod(
		"IsLiquidCentrifugeCooldownField", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CustomItemDataState.IsLiquidCentrifugeCooldownField not found.");

	private static readonly MethodInfo With = StateType.GetMethod(
		"WithLiquidCentrifugeCooldown", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CustomItemDataState.WithLiquidCentrifugeCooldown not found.");

	private static ComponentFieldMsg? CaptureField(string itemId, object[]? data) =>
		(ComponentFieldMsg?)Capture.Invoke(null, [itemId, data]);

	private static bool IsCooldownField(string itemId, ComponentFieldMsg field) =>
		(bool)IsField.Invoke(null, [itemId, field])!;

	private static object[] WithCooldown(string itemId, object[]? data, float value) =>
		(object[])With.Invoke(null, [itemId, data, value])!;

	[Fact]
	public void Capture_LiquidCentrifugeCooldown_ReturnsSyntheticFloatField()
	{
		var field = CaptureField("liquidcentrifuge", new object[] { 42.5f });

		Assert.NotNull(field);
		Assert.Equal("cooldown", field!.Name);
		Assert.Equal(SaveableFieldKind.Float, field.Kind);
		Assert.True(Math.Abs(field.FloatValue - 42.5f) < 0.001f,
			$"the cooldown value must ride as float, got {field.FloatValue}");
	}

	[Fact]
	public void Capture_NonLiquidCentrifuge_ReturnsNull() =>
		Assert.Null(CaptureField("waterbottle", new object[] { 1f }));

	[Fact]
	public void Capture_NullOrMissingData_UsesTheNativeDefaultZero()
	{
		var fromNull = CaptureField("liquidcentrifuge", null);
		var fromEmpty = CaptureField("liquidcentrifuge", []);

		Assert.NotNull(fromNull);
		Assert.Equal(0f, (float)fromNull!.FloatValue);
		Assert.NotNull(fromEmpty);
		Assert.Equal(0f, (float)fromEmpty!.FloatValue);
	}

	[Fact]
	public void Capture_NonFloatFirstElement_UsesTheNativeDefaultZero() =>
		Assert.Equal(0f, (float)CaptureField("liquidcentrifuge", new object[] { 1 })!.FloatValue);

	[Fact]
	public void IsCooldownField_OnlyMatchesLiquidCentrifugeFloatCooldown()
	{
		var match = new ComponentFieldMsg { Name = "cooldown", Kind = SaveableFieldKind.Float, FloatValue = 12f };

		Assert.True(IsCooldownField("liquidcentrifuge", match));
		Assert.False(IsCooldownField("jetpack", match));
		Assert.False(IsCooldownField("liquidcentrifuge",
			new ComponentFieldMsg { Name = "other", Kind = SaveableFieldKind.Float }));
		Assert.False(IsCooldownField("liquidcentrifuge",
			new ComponentFieldMsg { Name = "cooldown", Kind = SaveableFieldKind.Int }));
	}

	[Fact]
	public void With_LiquidCentrifugeMissingData_CreatesArrayAndSetsValue()
	{
		var data = WithCooldown("liquidcentrifuge", null, 30f);

		Assert.Single(data);
		Assert.Equal(30f, (float)data[0]);
	}

	[Fact]
	public void With_LiquidCentrifugeExistingData_MutatesTheSameArray()
	{
		var data = new object[] { 0f };
		var result = WithCooldown("liquidcentrifuge", data, 15f);

		Assert.Same(data, result);
		Assert.Equal(15f, (float)data[0]);
	}

	[Fact]
	public void GameField_CustomItemBehaviourDataRemainsPublicObjectArray()
	{
		var type = GameAssemblyHost.Game.GetType("CustomItemBehaviour", throwOnError: true)!;
		var field = type.GetField("data", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("CustomItemBehaviour.data not found.");

		Assert.True(field.FieldType == typeof(object[]),
			$"CustomItemBehaviour.data must stay object[], got {field.FieldType}.");
	}

	[Fact]
	public void Adapter_DeclaresCooldownRestoreMarker()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Items.LiquidCentrifugeCooldownRestore",
			throwOnError: true)!;

		var cooldown = type.GetField("Cooldown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("LiquidCentrifugeCooldownRestore.Cooldown not found.");
		Assert.True(cooldown.FieldType == typeof(float),
			$"the marker must carry float cooldown, got {cooldown.FieldType}.");

		var update = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("LiquidCentrifugeCooldownRestore.Update not found.");
		Assert.False(update.IsStatic);
	}
}
