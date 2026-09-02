using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The phase-3 typed limb physiology status projection DTO. Nullable fields
/// mean "do not touch this limb field", so round-trip must preserve both set and
/// unset values.
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

		var restored = ModLimbProjection.FromPayload(original.ToPayload());
		Assert.NotNull(restored);
		Assert.Equal(3.5f, restored!.BleedAmount);
		Assert.Equal(-2f, restored.SkinHealth);
		Assert.Null(restored.MuscleHealth);
		Assert.Equal(12f, restored.InfectionAmount);
	}

	[Fact]
	public void RoundTrip_PreservesAllUnsetFields()
	{
		var original = new ModLimbProjection();

		var restored = ModLimbProjection.FromPayload(original.ToPayload());
		Assert.NotNull(restored);
		Assert.Null(restored!.BleedAmount);
		Assert.Null(restored.SkinHealth);
		Assert.Null(restored.MuscleHealth);
		Assert.Null(restored.InfectionAmount);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModLimbProjection.FromPayload([]));
		Assert.Null(ModLimbProjection.FromPayload([1, 2, 3]));
		Assert.Null(ModLimbProjection.FromPayload(null!));
	}
}
