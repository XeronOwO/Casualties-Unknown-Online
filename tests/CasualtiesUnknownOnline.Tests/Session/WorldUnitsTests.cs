using CasualtiesUnknownOnline.Runtime.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The game's unit scale (the user's 2026-10-24 finding: two players a layer apart read as "1000 m"). The game's own
/// depth formula multiplies world units by 0.3 — `-(y - halfHeight) * 0.3f` — which is why one layer (2 × 512 units)
/// reads as ~307 m, so a CUO label showing raw units is 3.33× too large and disagrees with every depth the game prints.
/// </summary>
[Trait("Category", "Unit")]
public class WorldUnitsTests
{
	// The shipped halfHeight is 512 (read in the batches), so a layer is 1024 units.
	[Fact]
	public void ALayerOfTwoHalfHeights_IsAboutThreeHundredMeters() =>
		Assert.Equal(307.2f, WorldUnits.ToMeters(1024f), 1);

	[Fact]
	public void TheLayerHeightMatchesTheGamesOwnDepthFormula()
	{
		// WorldGeneration's formula: depth = -(y - halfHeight) * 0.3. A body at the bottom (y = -halfHeight) is one
		// layer deep; the same distance converted by this scale must agree.
		const float halfHeight = 512f;
		var bottom = -halfHeight;
		var nativeDepth = -(bottom - halfHeight) * 0.3f;
		Assert.Equal(nativeDepth, WorldUnits.ToMeters(halfHeight * 2f), 3);
	}

	[Fact]
	public void ADistanceOfOneThousandUnits_IsThreeHundredMetersNotAThousand() =>
		Assert.Equal(300, (int)WorldUnits.ToMeters(1000f));
}
