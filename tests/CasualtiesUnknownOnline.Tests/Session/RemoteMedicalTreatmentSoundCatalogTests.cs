using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The remote limb treatment's clip table — the clip the blocked native limb
/// action would have played, per accepted item. Every row is a DECISION: the
/// item's own delegate clip, the applied LIQUID's clip for the topical
/// containers (their delegate routes through <c>WaterContainerItem.ApplyToLimb</c>),
/// or a recorded silence with its reason. The gate
/// (<c>ItemAndBodySoundCaptureGateTests</c>) pins that no accepted item is
/// undecided; these tests pin what the decided rows SAY.
/// </summary>
public class RemoteMedicalTreatmentSoundCatalogTests
{
	[Theory]
	[InlineData("splint", "splint")]
	[InlineData("carcasssplint", "splint")]
	[InlineData("boneweldingtool", "boneweld")]
	[InlineData("clottingmush", "goo")]
	[InlineData("musharm", "goo")]
	[InlineData("chestdrain", "syringe")]
	[InlineData("tweezers", "tweezeruse")]
	[InlineData("wrench", "wrenchhit")]
	[InlineData("disinfectant", "spray")]
	[InlineData("spraybottle", "spray")]
	[InlineData("antiserum", "syringe")]
	[InlineData("bloodbag", "syringe")]
	[InlineData("bloodbaghuman", "syringe")]
	[InlineData("bloodcoagulant", "syringe")]
	[InlineData("combatpen", "syringe")]
	[InlineData("streptokinase", "syringe")]
	public void ALimbTreatmentItem_CarriesItsNativeLimbActionClip(string itemId, string clip)
	{
		Assert.True(RemoteMedicalTreatmentSoundCatalog.TryGetClip(itemId, out var actual));
		Assert.Equal(clip, actual);
	}

	[Fact]
	public void TheTopicalContainers_CarryTheirLiquidsClip()
	{
		Assert.True(RemoteMedicalTreatmentSoundCatalog.TryGetLiquidClip("reliefcream", out var relief));
		Assert.Equal("cream", relief);
		Assert.True(RemoteMedicalTreatmentSoundCatalog.TryGetLiquidClip("woundglue", out var glue));
		Assert.Equal("cream", glue);

		Assert.Contains("paincream", RemoteMedicalTreatmentSoundCatalog.LiquidDriven);
		Assert.Contains("woundglue", RemoteMedicalTreatmentSoundCatalog.LiquidDriven);
	}

	[Theory]
	[InlineData("syringe")] // the syringe-minigame items: 2D screen cues only
	[InlineData("morphine")]
	[InlineData("saline")]
	[InlineData("aed")]
	[InlineData("manualdefibrillator")]
	[InlineData("bandage")] // the bandage family: the native minigame's own step carries it
	[InlineData("rag")]
	[InlineData("alginate")]
	[InlineData("icepack")]
	[InlineData("tourniquet")]
	[InlineData("adhesivebandage")]
	[InlineData("makeshiftwrench")]
	[InlineData("medicalsuture")] // its delegate calls Body.DoGoreSound — a limb-presentation clip (todo/treatment-gore-presentation-not-carried.md)
	[InlineData("machete")] // the amputation's completion plays the limb's gore presentation — same ticket
	public void AnUncarriedItem_CarriesNoClipRow(string itemId)
	{
		Assert.Contains(itemId, RemoteMedicalTreatmentSoundCatalog.Uncarried);
		Assert.False(RemoteMedicalTreatmentSoundCatalog.TryGetClip(itemId, out _));
	}

	[Fact]
	public void NoItemIsBothUncarriedAndClipCarrying() =>
		Assert.Empty(RemoteMedicalTreatmentSoundCatalog.Uncarried.Where(id => RemoteMedicalTreatmentSoundCatalog.TryGetClip(id, out _)));

	[Fact]
	public void AnUnknownItem_IsNotInvented() =>
		Assert.False(RemoteMedicalTreatmentSoundCatalog.TryGetClip("not-a-medical-item", out _));
}
