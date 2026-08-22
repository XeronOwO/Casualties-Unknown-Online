using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Game-update guard for the GrapplingHook visual-state sync added in the
/// known-state-gap cleanup: the codec's explicit multiplayer-state table must
/// keep declaring the three grapple booleans, and the game assembly must keep
/// them as private bool fields. A game update that renames or retypes one
/// silently drops the fired/hookLatched/pulling state — this test fails before
/// it reaches the runtime.
/// </summary>
public class GrapplingHookComponentSyncContractTests
{
	private static readonly string[] GrappleFields = ["fired", "hookLatched", "pulling"];

	[Fact]
	public void CodecTable_DeclaresAllGrappleVisualStateFields()
	{
		var codec = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Items.ItemStateCodec",
			throwOnError: true)!;
		var tableField = codec.GetField("MultiplayerStateFields",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("ItemStateCodec.MultiplayerStateFields not found.");
		var table = (IDictionary)tableField.GetValue(null)!;

		Assert.True(table.Contains("GrapplingHook"),
			"ItemStateCodec must declare GrapplingHook in MultiplayerStateFields.");
		var declared = ((IEnumerable)table["GrapplingHook"]!)
			.Cast<string>()
			.OrderBy(x => x, StringComparer.Ordinal)
			.ToArray();
		Assert.Equal(GrappleFields, declared);
	}

	[Fact]
	public void GameFields_RemainPrivateBools()
	{
		var type = GameAssemblyHost.Game.GetType("GrapplingHook", throwOnError: true)!;
		foreach (var name in GrappleFields)
		{
			var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
				?? throw new InvalidOperationException($"GrapplingHook.{name} not found.");
			Assert.True(field.FieldType == typeof(bool), $"GrapplingHook.{name} must stay bool, got {field.FieldType}.");
			Assert.False(field.IsPublic, $"GrapplingHook.{name} must remain private (it is an owner-local gameplay flag, not a public API).");
		}
	}
}
