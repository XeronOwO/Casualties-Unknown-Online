using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure host-side radiation-line straggler policy. The Game Adapter
/// gathers local + remote entity-stream positions and this machine decides
/// whether the host should activate the line to pressure players who have not
/// yet reached the layer bottom. L0 coverage replaces manual dual-open
/// acceptance of the decision layer; the Unity boundary stays behind the
/// existing patch/field contracts.
/// </summary>
public class RadiationStragglerPolicyTests
{
	private const float BottomY = -100f;

	private static RadiationPlayerProgress Progress(float y, bool alive = true) => new(y, alive);

	[Fact]
	public void Activates_WhenOnePlayerReachedBottomAndAnotherStaysAbove()
	{
		var players = new[]
		{
			Progress(-105f),
			Progress(-90f),
		};

		Assert.True(RadiationStragglerPolicy.ShouldActivateLine(players, BottomY));
	}

	[Fact]
	public void DoesNotActivate_WhenEveryoneIsAtBottom()
	{
		var players = new[]
		{
			Progress(-105f),
			Progress(-120f),
		};

		Assert.False(RadiationStragglerPolicy.ShouldActivateLine(players, BottomY));
	}

	[Fact]
	public void DoesNotActivate_WhenEveryoneIsStillAboveTheBottom()
	{
		var players = new[]
		{
			Progress(-80f),
			Progress(-90f),
		};

		Assert.False(RadiationStragglerPolicy.ShouldActivateLine(players, BottomY));
	}

	[Fact]
	public void DoesNotActivate_WhenNoLivingPlayersRemain()
	{
		var players = new[]
		{
			Progress(-105f, alive: false),
			Progress(-90f, alive: false),
		};

		Assert.False(RadiationStragglerPolicy.ShouldActivateLine(players, BottomY));
	}

	[Fact]
	public void DeadPlayersDoNotCountAsStragglersOrLeaders()
	{
		// Two dead players straddle the line; only one living player remains above.
		var players = new[]
		{
			Progress(-105f, alive: false),
			Progress(-90f, alive: true),
			Progress(-80f, alive: false),
		};

		Assert.False(RadiationStragglerPolicy.ShouldActivateLine(players, BottomY));
	}

	[Fact]
	public void ExactlyAtTheBottomThresholdIsNotYetReached()
	{
		// The vanilla bottom trigger uses a strict < (WorldGeneration.cs:979);
		// exactly on the threshold should be treated as "still above" for the
		// pressure rule so the two sides use the same boundary.
		var players = new[]
		{
			Progress(BottomY),
			Progress(-90f),
		};

		Assert.False(RadiationStragglerPolicy.ShouldActivateLine(players, BottomY));
	}

	[Fact]
	public void OneLeaderIsEnough_EvenWithManyStragglers()
	{
		var players = new[]
		{
			Progress(-110f),
			Progress(-60f),
			Progress(-70f),
			Progress(-80f),
		};

		Assert.True(RadiationStragglerPolicy.ShouldActivateLine(players, BottomY));
	}

	[Fact]
	public void EmptyRoster_DoesNotActivate() =>
		Assert.False(RadiationStragglerPolicy.ShouldActivateLine([], BottomY));
}
