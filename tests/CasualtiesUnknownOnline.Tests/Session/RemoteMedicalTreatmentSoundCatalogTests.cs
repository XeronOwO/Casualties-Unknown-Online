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
	[InlineData("musharm", "goo")]
	[InlineData("tweezers", "tweezeruse")]
	[InlineData("wrench", "wrenchhit")]
	public void ASessionFamilyTool_CarriesTheClipItsBlockedNativeCallWouldHavePlayed(string itemId, string clip)
	{
		// The rows that are left serve the gestures whose native call the remote view
		// still blocks and no migrated chain runs: the wound-view bandage session's
		// musharm, and the shrapnel and dislocation sessions' held tools.
		Assert.True(RemoteMedicalTreatmentSoundCatalog.TryGetClip(itemId, out var actual));
		Assert.Equal(clip, actual);
	}

	[Theory]
	[InlineData("splint")]
	[InlineData("carcasssplint")]
	[InlineData("boneweldingtool")]
	[InlineData("clottingmush")]
	[InlineData("chestdrain")]
	[InlineData("medicalsuture")]
	[InlineData("icepack")]
	[InlineData("tourniquet")]
	public void ALimbTool_LeavesItsClipToItsOwnNativeAction(string itemId)
	{
		// Part B's last chain: the TREATED player's client runs the item's own
		// useLimbAction through NativeLimbToolApply, inside the medical capture scope,
		// so the delegate's own clip ("splint", "boneweld", "goo", the chest drain's
		// "syringe", the suture's gore roll) is relayed from the body it lands on. A row
		// here would double it, and the two tools whose delegate plays nothing
		// (icepack, tourniquet) need no recorded silence either — their native action
		// IS the decision now.
		Assert.False(RemoteMedicalTreatmentSoundCatalog.TryGetClip(itemId, out _), $"{itemId} must not carry a table row");
		Assert.DoesNotContain(itemId, RemoteMedicalTreatmentSoundCatalog.Uncarried);
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
