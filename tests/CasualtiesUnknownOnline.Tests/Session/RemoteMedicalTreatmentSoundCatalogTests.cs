using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The remote limb treatment's clip table — the clip the blocked native limb
/// action would have played, per accepted item. Every row is a DECISION: the
/// item's own delegate clip, or a recorded silence with its reason. The gate
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
	[InlineData("medicalsuture", "gore")] // its blocked delegate's first call is Body.DoGoreSound (Item.cs:378): the table names the base clip the limb's own Dismember plays, not one of the body's five rolled variants
	public void ALimbTreatmentItem_CarriesItsNativeLimbActionClip(string itemId, string clip)
	{
		Assert.True(RemoteMedicalTreatmentSoundCatalog.TryGetClip(itemId, out var actual));
		Assert.Equal(clip, actual);
	}

	[Theory]
	[InlineData("morphine")]
	[InlineData("saline")]
	[InlineData("syringe")]
	[InlineData("antiserum")]
	[InlineData("combatpen")]
	public void AnInjectableCarrier_IsDecidedByTheItemsOwnNativeActionRatherThanThisTable(string itemId)
	{
		// Part A of mod-cross-player-native-semantics: the operator's client RUNS
		// the item's own useLimbAction inside the medical capture window, so the
		// clip comes from the game's delegate and a row here would double it.
		Assert.False(RemoteMedicalTreatmentSoundCatalog.TryGetClip(itemId, out _), $"{itemId} must not carry a table row");
		Assert.DoesNotContain(itemId, RemoteMedicalTreatmentSoundCatalog.Uncarried);
	}

	[Theory]
	[InlineData("paincream")]
	[InlineData("woundglue")]
	[InlineData("disinfectant")]
	[InlineData("spraybottle")]
	public void ATopicalContainer_LeavesBothClipsToTheNativeCalls(string itemId)
	{
		// What this pins is the TABLE's half: it must stay silent for a topical
		// container, because both halves of that clip are native now — the spray
		// containers play "spray" in their own delegate (Item.cs:2100/2124), relayed
		// by the operator's measurement window, and the cream containers' clip
		// belongs to the liquid's onHealthUse (Liquids.cs:1074/1100), which runs on
		// the patient's own client through NativeTopicalApply and is relayed from
		// there. A row here would double the clip the patient plays locally. The
		// other half — that those native deciders exist and cover all four ids — is
		// pinned by ItemAndBodySoundCaptureGateTests (`VanillaTopicalCarriers` plus
		// the handler/patch source pins), which can read the tree.
		Assert.False(RemoteMedicalTreatmentSoundCatalog.TryGetClip(itemId, out _), $"{itemId} must not carry a table row");
		Assert.DoesNotContain(itemId, RemoteMedicalTreatmentSoundCatalog.Uncarried);
	}

	[Theory]
	[InlineData("aed")]
	[InlineData("manualdefibrillator")]
	[InlineData("bandage")] // the bandage family: the native minigame's own step carries it
	[InlineData("rag")]
	[InlineData("alginate")]
	[InlineData("icepack")]
	[InlineData("tourniquet")]
	[InlineData("adhesivebandage")]
	[InlineData("makeshiftwrench")]
	[InlineData("machete")] // the amputation's completion plays the limb's gore presentation, carried by the minigame step's own capture scope (AmputationMinigameSoundPatch)
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
