using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed status wire frame: a host-committed status becomes a
/// <see cref="ModStatusUpdate"/> value that a guest reads back and applies,
/// with no private format and no opaque bytes in between.
/// </summary>
public class ModStatusUpdateTests
{
	private static ModValue Sample() =>
		ModValue.Map(("hp", ModValue.Number(12.5)), ("tags", ModValue.List(ModValue.Text("a"), ModValue.Text("b"))));

	/// <summary>The frame's fields as the map factory's own shape, so a case can add or drop one.</summary>
	private static IEnumerable<(string Key, ModValue Value)> Fields(ModValue frame) =>
		frame.Fields!.Select(pair => (pair.Key, pair.Value));

	[Fact]
	public void BodySetRoundTrip_PreservesKeyScopeSchemaAndValue()
	{
		var original = ModStatusUpdate.ForBody("bleeding", 2001, 3, Sample());

		Assert.True(ModStatusUpdate.TryFromValue(original.ToValue(), out var restored));
		Assert.Equal("bleeding", restored.StatusId);
		Assert.Equal(ModStatusScope.Body, restored.Scope);
		Assert.Equal(2001UL, restored.PlayerSteamId);
		Assert.Equal(-1, restored.LimbSlot);
		Assert.Equal(3, restored.SchemaVersion);
		Assert.Equal(Sample(), restored.Value);
		Assert.False(restored.Remove);
	}

	[Fact]
	public void LimbRemoveRoundTrip_PreservesLimbSlotAndRemoveFlag()
	{
		var original = ModStatusUpdate.RemoveLimb("limb.bleed", 2001, 2, 4);

		Assert.True(ModStatusUpdate.TryFromValue(original.ToValue(), out var restored));
		Assert.Equal("limb.bleed", restored.StatusId);
		Assert.Equal(ModStatusScope.Limb, restored.Scope);
		Assert.Equal(2, restored.LimbSlot);
		Assert.Equal(4, restored.SchemaVersion);
		Assert.Null(restored.Value);
		Assert.True(restored.Remove);
	}

	[Fact]
	public void AValueThatIsNotAStatusFrame_IsRefused()
	{
		Assert.False(ModStatusUpdate.TryFromValue(null, out _));
		Assert.False(ModStatusUpdate.TryFromValue(ModValue.Text("not a frame"), out _));
		Assert.False(ModStatusUpdate.TryFromValue(ModValue.Map(), out _));
		Assert.False(ModStatusUpdate.TryFromValue(
			ModValue.Map((ModStatusUpdate.FieldId, ModValue.Text("bleeding"))), out _));
	}

	[Fact]
	public void ASetFrameWithoutAValue_IsRefused()
	{
		var frame = ModStatusUpdate.ForBody("bleeding", 2001, 1, Sample()).ToValue();
		var withoutValue = ModValue.Map([.. Fields(frame).Where(pair => pair.Key != ModStatusUpdate.FieldValue)]);

		Assert.False(ModStatusUpdate.TryFromValue(withoutValue, out _));
	}

	[Fact]
	public void AFieldTheReaderDoesNotKnow_IsIgnored()
	{
		var frame = ModStatusUpdate.ForBody("bleeding", 2001, 1, ModValue.Boolean(true)).ToValue();
		var widened = ModValue.Map([.. Fields(frame), ("from.a.newer.version", ModValue.Integer(7))]);

		Assert.True(ModStatusUpdate.TryFromValue(widened, out var restored));
		Assert.Equal("bleeding", restored.StatusId);
	}
}
