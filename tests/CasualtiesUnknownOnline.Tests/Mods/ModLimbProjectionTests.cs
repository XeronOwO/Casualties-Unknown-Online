using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The limb physiology status projection: an ABSENT field means "do not touch
/// this limb field", so round-trip must preserve both set and unset values, and
/// the field names the GameAdapter reads are pinned here.
/// </summary>
public class ModLimbProjectionTests
{
	[Fact]
	public void RoundTrip_PreservesSetOptionalFields()
	{
		var original = new ModLimbProjection
		{
			BleedAmount = 3.5f,
			SkinHealth = -2f,
			MuscleHealth = null,
			InfectionAmount = 12f,
		};

		Assert.True(ModLimbProjection.TryFromValue(original.ToValue(), out var restored));
		Assert.Equal(3.5f, restored.BleedAmount);
		Assert.Equal(-2f, restored.SkinHealth);
		Assert.Null(restored.MuscleHealth);
		Assert.Equal(12f, restored.InfectionAmount);
	}

	[Fact]
	public void RoundTrip_PreservesAllUnsetFields()
	{
		var original = new ModLimbProjection();

		Assert.True(ModLimbProjection.TryFromValue(original.ToValue(), out var restored));
		Assert.Null(restored.BleedAmount);
		Assert.Null(restored.SkinHealth);
		Assert.Null(restored.MuscleHealth);
		Assert.Null(restored.InfectionAmount);
	}

	[Fact]
	public void AnUnsetField_IsAbsentFromTheValue()
	{
		var value = new ModLimbProjection { SkinHealth = 1f }.ToValue();

		Assert.Equal(ModValueKind.Map, value.Kind);
		Assert.True(value.TryGetField(ModLimbProjection.FieldSkinHealth, out _));
		Assert.False(value.TryGetField(ModLimbProjection.FieldMuscleHealth, out _));
	}

	[Fact]
	public void InvalidValue_IsRefused()
	{
		Assert.False(ModLimbProjection.TryFromValue(null, out _));
		Assert.False(ModLimbProjection.TryFromValue(ModValue.Integer(1), out _));
		Assert.False(ModLimbProjection.TryFromValue(
			ModValue.Map((ModLimbProjection.FieldBleedAmount, ModValue.Boolean(true))), out _));
	}
}
