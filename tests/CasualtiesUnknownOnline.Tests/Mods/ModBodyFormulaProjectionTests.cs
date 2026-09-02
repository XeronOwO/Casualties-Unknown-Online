using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The phase-3 typed body-formula status projection DTO. It is a plain
/// game-free payload that the GameAdapter decodes from an opaque status value.
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

		var restored = ModBodyFormulaProjection.FromPayload(original.ToPayload());
		Assert.NotNull(restored);
		Assert.Equal(2.5f, restored!.MaxEncumbrance);
		Assert.Equal(-1.25f, restored.TotalEncumbrance);
		Assert.Equal(10f, restored.Immunity);
		Assert.Equal(3f, restored.JumpSpeed);
		Assert.Equal(-5f, restored.AveragePain);
		Assert.Equal(12.5f, restored.HeartRateOffset);
		Assert.Equal(-3.75f, restored.RespiratoryRateOffset);
		Assert.Equal(8f, restored.BloodPressureOffset);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModBodyFormulaProjection.FromPayload([]));
		Assert.Null(ModBodyFormulaProjection.FromPayload([1, 2, 3]));
		Assert.Null(ModBodyFormulaProjection.FromPayload(null!));
	}
}
