using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The body-formula status projection: a game-free value the GameAdapter reads
/// out of a mod's status slot, with the field names it reads pinned here.
/// </summary>
public class ModBodyFormulaProjectionTests
{
	[Fact]
	public void RoundTrip_PreservesAllContributions()
	{
		var original = new ModBodyFormulaProjection
		{
			MaxEncumbrance = 2.5f,
			TotalEncumbrance = -1.25f,
			Immunity = 10f,
			JumpSpeed = 3f,
			AveragePain = -5f,
			HeartRateOffset = 12.5f,
			RespiratoryRateOffset = -3.75f,
			BloodPressureOffset = 8f,
		};

		Assert.True(ModBodyFormulaProjection.TryFromValue(original.ToValue(), out var restored));
		Assert.Equal(2.5f, restored.MaxEncumbrance);
		Assert.Equal(-1.25f, restored.TotalEncumbrance);
		Assert.Equal(10f, restored.Immunity);
		Assert.Equal(3f, restored.JumpSpeed);
		Assert.Equal(-5f, restored.AveragePain);
		Assert.Equal(12.5f, restored.HeartRateOffset);
		Assert.Equal(-3.75f, restored.RespiratoryRateOffset);
		Assert.Equal(8f, restored.BloodPressureOffset);
	}

	[Fact]
	public void Value_IsAMapUnderTheDocumentedFieldNames()
	{
		var value = new ModBodyFormulaProjection { Immunity = 10f }.ToValue();

		Assert.Equal(ModValueKind.Map, value.Kind);
		Assert.True(value.TryGetField(ModBodyFormulaProjection.FieldImmunity, out var immunity));
		Assert.True(immunity.TryGetNumber(out var number));
		Assert.Equal(10d, number);
	}

	[Fact]
	public void AnAbsentField_ReadsAsZero()
	{
		Assert.True(ModBodyFormulaProjection.TryFromValue(ModValue.Map(), out var projection));

		Assert.Equal(0f, projection.Immunity);
		Assert.Equal(0f, projection.BloodPressureOffset);
	}

	[Fact]
	public void InvalidValue_IsRefused()
	{
		Assert.False(ModBodyFormulaProjection.TryFromValue(null, out _));
		Assert.False(ModBodyFormulaProjection.TryFromValue(ModValue.Text("not a projection"), out _));
		Assert.False(ModBodyFormulaProjection.TryFromValue(ModValue.List(), out _));
		Assert.False(ModBodyFormulaProjection.TryFromValue(
			ModValue.Map((ModBodyFormulaProjection.FieldImmunity, ModValue.Text("ten"))), out _));
	}
}
