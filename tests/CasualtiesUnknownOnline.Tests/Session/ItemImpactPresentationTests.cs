using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure presentation decision a received world-item impact event replays:
/// an item landing replays the native <c>drop</c> clip, the landing block's
/// step sound and the dust (Item.cs:238-247); a plush replays only its own
/// squeak (PlushScript.cs:17-23). An unknown kind presents NOTHING — a code for
/// a presentation this side does not know must not invent one.
/// </summary>
public class ItemImpactPresentationTests
{
	[Fact]
	public void AnItemLanding_ReplaysTheNativeDropStepAndDust()
	{
		var presentation = ItemImpactPresentation.Of(ItemImpactKind.ItemImpact);

		Assert.True(presentation.PlaysDropClip);
		Assert.True(presentation.PlaysLandingStepClip);
		Assert.True(presentation.SpawnsDust);
		Assert.False(presentation.PlaysOwnClip);
	}

	[Fact]
	public void APlushSqueak_ReplaysOnlyItsOwnClip()
	{
		var presentation = ItemImpactPresentation.Of(ItemImpactKind.PlushSqueak);

		Assert.False(presentation.PlaysDropClip);
		Assert.False(presentation.PlaysLandingStepClip);
		Assert.False(presentation.SpawnsDust);
		Assert.True(presentation.PlaysOwnClip);
	}

	[Fact]
	public void AnUnknownKind_PresentsNothing()
	{
		var presentation = ItemImpactPresentation.Of((ItemImpactKind)200);

		Assert.False(presentation.PlaysDropClip);
		Assert.False(presentation.PlaysLandingStepClip);
		Assert.False(presentation.SpawnsDust);
		Assert.False(presentation.PlaysOwnClip);
	}
}
